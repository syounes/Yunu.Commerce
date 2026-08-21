using Xunit;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Concurrency integration tests for <see cref="MongoProductSkuConcurrencyCoordinator"/>
/// against a real, single-node MongoDB replica set
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
///
/// These tests exist because the existing Application-layer unit tests use
/// <c>FakeProductSkuConcurrencyCoordinator</c>, an in-memory fake that cannot
/// prove the actual MongoDB transaction/`LifecycleRevision` coordination
/// behaves correctly under real concurrent writers. Every scenario here
/// exercises the single invariant the coordinator exists to protect:
///
///     Product.Status == Archived
///         =&gt;
///     no Sku belonging to that Product has a Status other than Archived.
///
/// Concurrency is driven with real overlapping `Task`s racing against the
/// same underlying documents; a <see cref="Barrier"/> is used to align both
/// operations at the same starting instant so they genuinely contend for the
/// same MongoDB transaction/write, rather than executing sequentially.
/// </summary>
[Collection(nameof(MongoReplicaSetCollection))]
public sealed class MongoProductSkuConcurrencyCoordinatorTests
{
    private readonly MongoReplicaSetFixture _fixture;

    public MongoProductSkuConcurrencyCoordinatorTests(MongoReplicaSetFixture fixture)
    {
        _fixture = fixture;
    }

    private static CanonicalTaxonomyNodeId CreateCanonicalTaxonomyNodeId() => new(1234);

    private async Task<Product> SeedProductAsync(ProductStatus status)
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName($"Concurrency Test Product {Guid.NewGuid()}"),
            description: null,
            BrandId.New(),
            CreateCanonicalTaxonomyNodeId(),
            status);

        await _fixture.ProductRepository.AddAsync(product, CancellationToken.None);

        return product;
    }

    private async Task<Sku> SeedSkuAsync(ProductId productId, SkuStatus status)
    {
        var sku = Sku.Create(SkuId.New(), productId, new SkuCode($"SKU-{Guid.NewGuid():N}"), status: status);

        await _fixture.SkuRepository.AddAsync(sku, CancellationToken.None);

        return sku;
    }

    private async Task<ProductDocumentSnapshot> ReadPersistedStateAsync(ProductId productId, SkuId? skuId)
    {
        var product = await _fixture.ProductRepository.GetByIdAsync(productId, CancellationToken.None);
        var sku = skuId.HasValue
            ? await _fixture.SkuRepository.GetByIdAsync(skuId.Value, CancellationToken.None)
            : null;

        return new ProductDocumentSnapshot(product, sku);
    }

    private sealed record ProductDocumentSnapshot(Product? Product, Sku? Sku);

    /// <summary>
    /// Runs two operations concurrently, releasing both only once each has
    /// reached the barrier, so they genuinely overlap instead of running
    /// sequentially.
    /// </summary>
    private static async Task<(TA A, TB B)> RunConcurrentlyAsync<TA, TB>(
        Func<Barrier, Task<TA>> operationA,
        Func<Barrier, Task<TB>> operationB)
    {
        var barrier = new Barrier(2);

        var taskA = Task.Run(() => operationA(barrier));
        var taskB = Task.Run(() => operationB(barrier));

        await Task.WhenAll(taskA, taskB);

        return (taskA.Result, taskB.Result);
    }

    /// <summary>
    /// Scenario A — Product Archive vs concurrent Sku creation.
    ///
    /// Proves: the committed final state can never be
    /// "Product = Archived AND a new Sku exists with Status != Archived".
    /// Either operation may legitimately win the race.
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_Concurrent_With_CreateSku_Should_Never_Violate_Invariant()
    {
        var product = await SeedProductAsync(ProductStatus.Active);
        var newSku = Sku.Create(SkuId.New(), product.Id, new SkuCode($"SKU-{Guid.NewGuid():N}"), status: SkuStatus.Draft);

        var (archiveResult, createResult) = await RunConcurrentlyAsync(
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);
            },
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.CreateSkuIfProductNotArchivedAsync(newSku, CancellationToken.None);
            });

        Assert.Contains(archiveResult, new[] { ArchiveProductCoordinationResult.Archived, ArchiveProductCoordinationResult.NonArchivedSkuExists, ArchiveProductCoordinationResult.ConcurrencyConflict });
        Assert.Contains(createResult, new[] { CreateSkuCoordinationResult.Created, CreateSkuCoordinationResult.ProductArchived });

        var persistedProduct = await _fixture.ProductRepository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.NotNull(persistedProduct);

        if (persistedProduct!.Status == ProductStatus.Archived)
        {
            var persistedSku = await _fixture.SkuRepository.GetByIdAsync(newSku.Id, CancellationToken.None);
            Assert.True(
                persistedSku is null || persistedSku.Status == SkuStatus.Archived,
                "Invariant violated: Product is Archived but a non-Archived Sku exists.");
        }
    }

    /// <summary>
    /// Scenario B — Product Archive vs concurrent Sku reactivation
    /// (Inactive -&gt; Active).
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_Concurrent_With_ReactivateSku_Should_Never_Violate_Invariant()
    {
        var product = await SeedProductAsync(ProductStatus.Active);
        var sku = await SeedSkuAsync(product.Id, SkuStatus.Inactive);

        var (archiveResult, transitionResult) = await RunConcurrentlyAsync(
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);
            },
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.TransitionSkuIfProductNotArchivedAsync(
                    sku.Id, SkuStatus.Inactive, SkuStatus.Active, CancellationToken.None);
            });

        Assert.Contains(archiveResult, new[] { ArchiveProductCoordinationResult.Archived, ArchiveProductCoordinationResult.NonArchivedSkuExists, ArchiveProductCoordinationResult.ConcurrencyConflict });
        Assert.Contains(transitionResult, new[] { SkuTransitionCoordinationResult.Transitioned, SkuTransitionCoordinationResult.ProductArchived });

        var state = await ReadPersistedStateAsync(product.Id, sku.Id);
        Assert.NotNull(state.Product);
        Assert.NotNull(state.Sku);

        Assert.False(
            state.Product!.Status == ProductStatus.Archived && state.Sku!.Status == SkuStatus.Active,
            "Invariant violated: Product is Archived but Sku is Active.");
    }

    /// <summary>
    /// Scenario C — Product Archive vs concurrent Sku transition to Inactive
    /// (Active -&gt; Inactive). Even though the resulting Sku status
    /// (Inactive) is itself non-Archived, the same cross-aggregate invariant
    /// must hold for the persisted final state.
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_Concurrent_With_Sku_Active_To_Inactive_Should_Never_Violate_Invariant()
    {
        var product = await SeedProductAsync(ProductStatus.Active);
        var sku = await SeedSkuAsync(product.Id, SkuStatus.Active);

        var (archiveResult, transitionResult) = await RunConcurrentlyAsync(
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);
            },
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.TransitionSkuIfProductNotArchivedAsync(
                    sku.Id, SkuStatus.Active, SkuStatus.Inactive, CancellationToken.None);
            });

        Assert.Contains(archiveResult, new[] { ArchiveProductCoordinationResult.Archived, ArchiveProductCoordinationResult.NonArchivedSkuExists, ArchiveProductCoordinationResult.ConcurrencyConflict });
        Assert.Contains(transitionResult, new[] { SkuTransitionCoordinationResult.Transitioned, SkuTransitionCoordinationResult.ProductArchived });

        var state = await ReadPersistedStateAsync(product.Id, sku.Id);
        Assert.NotNull(state.Product);
        Assert.NotNull(state.Sku);

        if (state.Product!.Status == ProductStatus.Archived)
        {
            Assert.Equal(SkuStatus.Archived, state.Sku!.Status);
        }
    }

    /// <summary>
    /// Scenario D — Create Sku under an already Archived Product.
    /// </summary>
    [Fact]
    public async Task CreateSku_Under_Already_Archived_Product_Should_Be_Rejected()
    {
        var product = await SeedProductAsync(ProductStatus.Archived);
        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode($"SKU-{Guid.NewGuid():N}"));

        var result = await _fixture.Coordinator.CreateSkuIfProductNotArchivedAsync(sku, CancellationToken.None);

        Assert.Equal(CreateSkuCoordinationResult.ProductArchived, result);

        var persistedSku = await _fixture.SkuRepository.GetByIdAsync(sku.Id, CancellationToken.None);
        Assert.Null(persistedSku);
    }

    /// <summary>
    /// Scenario E — Reactivate a Sku under an already Archived Product.
    /// </summary>
    [Fact]
    public async Task Reactivate_Sku_Under_Already_Archived_Product_Should_Be_Rejected()
    {
        var product = await SeedProductAsync(ProductStatus.Archived);
        var sku = await SeedSkuAsync(product.Id, SkuStatus.Inactive);

        var result = await _fixture.Coordinator.TransitionSkuIfProductNotArchivedAsync(
            sku.Id, SkuStatus.Inactive, SkuStatus.Active, CancellationToken.None);

        Assert.Equal(SkuTransitionCoordinationResult.ProductArchived, result);

        var persistedSku = await _fixture.SkuRepository.GetByIdAsync(sku.Id, CancellationToken.None);
        Assert.NotNull(persistedSku);
        Assert.Equal(SkuStatus.Inactive, persistedSku!.Status);
    }

    /// <summary>
    /// Scenario F — Product archive while a non-Archived Sku already exists
    /// (no concurrency involved: proves the plain, non-racing guard too).
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_With_Existing_NonArchived_Sku_Should_Be_Rejected()
    {
        var product = await SeedProductAsync(ProductStatus.Active);
        var sku = await SeedSkuAsync(product.Id, SkuStatus.Active);

        var result = await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);

        Assert.Equal(ArchiveProductCoordinationResult.NonArchivedSkuExists, result);

        var persistedProduct = await _fixture.ProductRepository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.NotNull(persistedProduct);
        Assert.Equal(ProductStatus.Active, persistedProduct!.Status);

        var persistedSku = await _fixture.SkuRepository.GetByIdAsync(sku.Id, CancellationToken.None);
        Assert.NotNull(persistedSku);
        Assert.Equal(SkuStatus.Active, persistedSku!.Status);
    }

    /// <summary>
    /// Scenario G — LifecycleRevision optimistic-concurrency conflict.
    ///
    /// Proves against real MongoDB that a stale Product lifecycle write
    /// (one that read the Product before another writer already bumped
    /// LifecycleRevision) cannot silently overwrite the newer state: the
    /// stale attempt must observe ConcurrencyConflict, not a lost update.
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_With_Stale_Expected_Status_Should_Not_Cause_Lost_Update()
    {
        var product = await SeedProductAsync(ProductStatus.Active);

        // First writer legitimately transitions Active -> Inactive, bumping
        // LifecycleRevision via the same repository path used in production.
        var firstWriterSucceeded = await _fixture.ProductRepository.UpdateStatusAsync(
            product.Id, ProductStatus.Active, ProductStatus.Inactive, CancellationToken.None);
        Assert.True(firstWriterSucceeded);

        // A second, stale writer still believes the Product is Active (its
        // in-memory read predates the first writer's commit) and attempts to
        // archive it directly against that stale expectation.
        var staleResult = await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);

        Assert.Equal(ArchiveProductCoordinationResult.ConcurrencyConflict, staleResult);

        var persistedProduct = await _fixture.ProductRepository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.NotNull(persistedProduct);
        Assert.Equal(ProductStatus.Inactive, persistedProduct!.Status);
    }
}
