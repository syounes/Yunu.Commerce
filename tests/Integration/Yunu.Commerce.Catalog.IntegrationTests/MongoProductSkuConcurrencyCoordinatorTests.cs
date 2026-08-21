using Xunit;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

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
/// Test classification:
/// - Genuine concurrency races (both operations can independently observe a
///   valid precondition and race toward the forbidden final state):
///   Scenario A (Archive vs CreateSku, the primary write-skew proof) and
///   Scenario G (two concurrent Archive attempts, proving first-writer-wins;
///   it does not claim both transactions read the same LifecycleRevision
///   before either committed).
/// - Deterministic guards (the outcome cannot depend on timing because one
///   side's precondition check already fails regardless of interleaving):
///   Scenarios B/C/D/E/F. A Sku that already exists and is non-Archived
///   before ArchiveProductAsync's own check runs guarantees
///   NonArchivedSkuExists; these are kept as integration coverage of the
///   guards themselves, not as concurrency proofs.
/// - A dedicated deterministic test proves that CreateSku and ArchiveProduct
///   (opposite sides of the invariant) both increment the same persisted
///   `ProductDocument.LifecycleRevision` coordination token.
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
    /// Scenario B (guard, not a race) — Archiving a Product that already has
    /// an Inactive Sku must be rejected deterministically. Unlike Scenario A
    /// (CreateSku), the Sku already exists before ArchiveProductAsync's own
    /// non-Archived-Sku check runs, so this can never be a genuine write-skew
    /// race: NonArchivedSkuExists is guaranteed to be observed regardless of
    /// timing. Kept as an integration test of the guard itself, not
    /// classified as a concurrency scenario.
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_With_Existing_Inactive_Sku_Should_Be_Rejected()
    {
        var product = await SeedProductAsync(ProductStatus.Active);
        var sku = await SeedSkuAsync(product.Id, SkuStatus.Inactive);

        var archiveResult = await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);

        Assert.Equal(ArchiveProductCoordinationResult.NonArchivedSkuExists, archiveResult);

        var state = await ReadPersistedStateAsync(product.Id, sku.Id);
        Assert.NotNull(state.Product);
        Assert.NotNull(state.Sku);
        Assert.Equal(ProductStatus.Active, state.Product!.Status);
        Assert.Equal(SkuStatus.Inactive, state.Sku!.Status);
    }

    /// <summary>
    /// Scenario C (guard, not a race) — Archiving a Product that already has
    /// an Active Sku must be rejected deterministically, and a concurrent
    /// Active -&gt; Inactive Sku transition does not change that outcome: the
    /// Sku is already non-Archived before ArchiveProductAsync's own check
    /// runs (either it observes Active or, if the transition to Inactive
    /// commits first, it observes Inactive — both are non-Archived). This is
    /// not a genuine write-skew race like Scenario A because no interleaving
    /// of these two operations can ever produce a state where the Sku looks
    /// Archived to the Archive check.
    /// </summary>
    [Fact]
    public async Task ArchiveProduct_Concurrent_With_Sku_Active_To_Inactive_Should_Always_Be_Rejected()
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

        Assert.Equal(ArchiveProductCoordinationResult.NonArchivedSkuExists, archiveResult);
        Assert.Equal(SkuTransitionCoordinationResult.Transitioned, transitionResult);

        var state = await ReadPersistedStateAsync(product.Id, sku.Id);
        Assert.NotNull(state.Product);
        Assert.NotNull(state.Sku);
        Assert.Equal(ProductStatus.Active, state.Product!.Status);
        Assert.Equal(SkuStatus.Inactive, state.Sku!.Status);
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
    /// Scenario G — concurrent Product Archive attempts, first-writer-wins.
    ///
    /// Two <c>ArchiveProductAsync</c> operations are launched concurrently
    /// against the same Product, synchronized via a <see cref="Barrier"/>
    /// immediately before each calls <c>ArchiveProductAsync</c>. This proves
    /// the observable outcome: only one of the two operations successfully
    /// archives the Product; the competing operation returns
    /// `ConcurrencyConflict`; the final persisted Product state is
    /// `Archived`. It does NOT prove — and does not claim — that both
    /// transactions were guaranteed to read the same
    /// <c>ProductDocument.LifecycleRevision</c> before either committed: the
    /// Barrier only aligns the two calls' starting instant, not their
    /// in-transaction reads, so one transaction may already have committed
    /// before the other performs its first transactional read. The
    /// deterministic proof that both sides of the cross-aggregate invariant
    /// touch the same persisted `LifecycleRevision` token is provided
    /// separately (see the dedicated LifecycleRevision participation test).
    /// </summary>
    [Fact]
    public async Task Concurrent_ArchiveProduct_Attempts_Should_Be_FirstWriterWins()
    {
        var product = await SeedProductAsync(ProductStatus.Active);

        var (resultA, resultB) = await RunConcurrentlyAsync(
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);
            },
            async barrier =>
            {
                barrier.SignalAndWait();
                return await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);
            });

        var results = new[] { resultA, resultB };

        Assert.Single(results, r => r == ArchiveProductCoordinationResult.Archived);
        Assert.Single(results, r => r == ArchiveProductCoordinationResult.ConcurrencyConflict);

        var persistedProduct = await _fixture.ProductRepository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.NotNull(persistedProduct);
        Assert.Equal(ProductStatus.Archived, persistedProduct!.Status);
    }

    /// <summary>
    /// Deterministic proof that protected cross-aggregate operations on
    /// opposite sides of the Product/Sku invariant (CreateSku and
    /// ArchiveProduct) actually increment the same persisted
    /// <c>ProductDocument.LifecycleRevision</c> coordination token, by
    /// inspecting the raw Mongo document directly through the test's own
    /// Mongo client/collection (infrastructure-only; never exposed through
    /// the Domain <c>Product</c> Aggregate or any DTO). This is not a test of
    /// MongoDB itself: it proves that CreateSku and ArchiveProduct share a
    /// genuine common write point instead of relying on independent,
    /// uncoordinated document writes.
    /// </summary>
    [Fact]
    public async Task LifecycleRevision_Should_Increase_Across_CreateSku_And_ArchiveProduct()
    {
        var product = await SeedProductAsync(ProductStatus.Active);

        var products = _fixture.MongoClient
            .GetDatabase("yunu_catalog_concurrency_tests")
            .GetCollection<ProductDocument>("products");

        var initialRevision = (await products.Find(p => p.Id == product.Id.Value).FirstAsync()).LifecycleRevision;

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode($"SKU-{Guid.NewGuid():N}"), status: SkuStatus.Draft);
        var createResult = await _fixture.Coordinator.CreateSkuIfProductNotArchivedAsync(sku, CancellationToken.None);
        Assert.Equal(CreateSkuCoordinationResult.Created, createResult);

        var revisionAfterCreateSku = (await products.Find(p => p.Id == product.Id.Value).FirstAsync()).LifecycleRevision;
        Assert.True(
            revisionAfterCreateSku > initialRevision,
            $"Expected LifecycleRevision to increase after CreateSku (was {initialRevision}, now {revisionAfterCreateSku}).");

        var archiveSkuResult = await _fixture.Coordinator.TransitionSkuIfProductNotArchivedAsync(
            sku.Id, SkuStatus.Draft, SkuStatus.Archived, CancellationToken.None);
        Assert.Equal(SkuTransitionCoordinationResult.Transitioned, archiveSkuResult);

        var archiveResult = await _fixture.Coordinator.ArchiveProductAsync(product.Id, ProductStatus.Active, CancellationToken.None);
        Assert.Equal(ArchiveProductCoordinationResult.Archived, archiveResult);

        var revisionAfterArchiveProduct = (await products.Find(p => p.Id == product.Id.Value).FirstAsync()).LifecycleRevision;
        Assert.True(
            revisionAfterArchiveProduct > revisionAfterCreateSku,
            $"Expected LifecycleRevision to increase again after ArchiveProduct (was {revisionAfterCreateSku}, now {revisionAfterArchiveProduct}).");
    }
}
