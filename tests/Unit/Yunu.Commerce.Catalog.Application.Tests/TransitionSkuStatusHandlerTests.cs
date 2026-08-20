using Xunit;
using Yunu.Commerce.Catalog.Application.Skus;
using Yunu.Commerce.Catalog.Application.Skus.TransitionSkuStatus;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class TransitionSkuStatusHandlerTests
{
    private static Product CreateProduct()
    {
        return Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            description: null,
            BrandId.New(),
            new CanonicalTaxonomyNodeId(1234));
    }

    [Fact]
    public async Task HandleAsync_Should_Transition_Sku_Status()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new TransitionSkuStatusHandler(skuRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await handler.HandleAsync(new TransitionSkuStatusCommand
        {
            SkuId = sku.Id.Value,
            Status = nameof(SkuStatus.Active)
        }, CancellationToken.None);

        var reloaded = await skuRepository.GetByIdAsync(sku.Id, CancellationToken.None);
        Assert.Equal(SkuStatus.Active, reloaded!.Status);
    }

    [Fact]
    public async Task HandleAsync_Activate_While_Product_Archived_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        product.TransitionTo(ProductStatus.Archived);
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new TransitionSkuStatusHandler(skuRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<ProductArchivedException>(() => handler.HandleAsync(
            new TransitionSkuStatusCommand { SkuId = sku.Id.Value, Status = nameof(SkuStatus.Active) },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Discontinue_While_Product_Archived_Should_Succeed()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        product.TransitionTo(ProductStatus.Archived);
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new TransitionSkuStatusHandler(skuRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await handler.HandleAsync(new TransitionSkuStatusCommand
        {
            SkuId = sku.Id.Value,
            Status = nameof(SkuStatus.Archived)
        }, CancellationToken.None);

        var reloaded = await skuRepository.GetByIdAsync(sku.Id, CancellationToken.None);
        Assert.Equal(SkuStatus.Archived, reloaded!.Status);
    }

    [Fact]
    public async Task HandleAsync_With_NonExistent_Sku_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var handler = new TransitionSkuStatusHandler(skuRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(
            new TransitionSkuStatusCommand { SkuId = Guid.NewGuid(), Status = nameof(SkuStatus.Active) },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Draft_To_Inactive_Should_Throw_InvalidTransition()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new TransitionSkuStatusHandler(skuRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<InvalidSkuStatusTransitionException>(() => handler.HandleAsync(
            new TransitionSkuStatusCommand { SkuId = sku.Id.Value, Status = nameof(SkuStatus.Inactive) },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_When_Concurrent_Writer_Wins_Race_Should_Throw_ConcurrencyConflict_Not_Reinterpret()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var coordinator = new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository);
        var handlerA = new TransitionSkuStatusHandler(skuRepository, coordinator);

        // Worker B loads the Draft Sku state before Worker A commits, so its
        // own view of "current status" stays stale even after Worker A writes.
        var staleSkuRepository = new StaleReadSkuRepository(skuRepository);
        var handlerB = new TransitionSkuStatusHandler(staleSkuRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        // Worker B loads its (Draft) view first, before Worker A commits.
        await staleSkuRepository.GetByIdAsync(sku.Id, CancellationToken.None);

        // Both "workers" load the same Draft Sku state independently. Worker
        // A commits Draft -> Active first.
        await handlerA.HandleAsync(
            new TransitionSkuStatusCommand { SkuId = sku.Id.Value, Status = nameof(SkuStatus.Active) },
            CancellationToken.None);

        // Worker B's own conditional write (still expecting Draft) must lose
        // the race and fail explicitly rather than reload and reinterpret.
        await Assert.ThrowsAsync<SkuStatusConcurrencyConflictException>(() => handlerB.HandleAsync(
            new TransitionSkuStatusCommand { SkuId = sku.Id.Value, Status = nameof(SkuStatus.Active) },
            CancellationToken.None));
    }
}

/// <summary>
/// Test-only decorator that always returns the Sku state as it was on its
/// first read, simulating a worker that loaded its view before a concurrent
/// writer committed a change. Write operations are delegated to the shared
/// underlying repository so both "workers" observe the same persisted state.
/// </summary>
internal sealed class StaleReadSkuRepository : ISkuRepository
{
    private readonly ISkuRepository _inner;
    private Sku? _staleSnapshot;

    public StaleReadSkuRepository(ISkuRepository inner)
    {
        _inner = inner;
    }

    public async Task<Sku?> GetByIdAsync(SkuId id, CancellationToken cancellationToken)
    {
        if (_staleSnapshot is not null)
        {
            return _staleSnapshot;
        }

        _staleSnapshot = await _inner.GetByIdAsync(id, cancellationToken);
        return _staleSnapshot;
    }

    public Task AddAsync(Sku sku, CancellationToken cancellationToken) => _inner.AddAsync(sku, cancellationToken);

    public Task<IReadOnlyCollection<Sku>> GetByProductIdAsync(ProductId productId, CancellationToken cancellationToken)
        => _inner.GetByProductIdAsync(productId, cancellationToken);

    public Task<bool> ExistsBySegmentDefinitionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId, CancellationToken cancellationToken)
        => _inner.ExistsBySegmentDefinitionIdAsync(segmentDefinitionId, cancellationToken);

    public Task<bool> ExistsBySegmentOptionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId, CancellationToken cancellationToken)
        => _inner.ExistsBySegmentOptionIdAsync(segmentOptionId, cancellationToken);

    public Task<bool> UpdateStatusAsync(SkuId id, SkuStatus expectedCurrentStatus, SkuStatus newStatus, CancellationToken cancellationToken)
        => _inner.UpdateStatusAsync(id, expectedCurrentStatus, newStatus, cancellationToken);

    public Task<bool> ExistsNonArchivedByProductIdAsync(ProductId productId, CancellationToken cancellationToken)
        => _inner.ExistsNonArchivedByProductIdAsync(productId, cancellationToken);
}
