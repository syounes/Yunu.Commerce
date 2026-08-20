using Xunit;
using Yunu.Commerce.Catalog.Application.Products;
using Yunu.Commerce.Catalog.Application.Products.TransitionProductStatus;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class TransitionProductStatusHandlerTests
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
    public async Task HandleAsync_Should_Transition_Product_Status()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = new TransitionProductStatusHandler(productRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await handler.HandleAsync(new TransitionProductStatusCommand
        {
            ProductId = product.Id.Value,
            Status = nameof(ProductStatus.Active)
        }, CancellationToken.None);

        var reloaded = await productRepository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.Equal(ProductStatus.Active, reloaded!.Status);
    }

    [Fact]
    public async Task HandleAsync_Archiving_With_NonArchived_Sku_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new TransitionProductStatusHandler(productRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<ProductHasNonArchivedSkusException>(() => handler.HandleAsync(
            new TransitionProductStatusCommand
            {
                ProductId = product.Id.Value,
                Status = nameof(ProductStatus.Archived)
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_Archiving_With_Only_Archived_Skus_Should_Succeed()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        sku.Discontinue();
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new TransitionProductStatusHandler(productRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await handler.HandleAsync(new TransitionProductStatusCommand
        {
            ProductId = product.Id.Value,
            Status = nameof(ProductStatus.Archived)
        }, CancellationToken.None);

        var reloaded = await productRepository.GetByIdAsync(product.Id, CancellationToken.None);
        Assert.Equal(ProductStatus.Archived, reloaded!.Status);
    }

    [Fact]
    public async Task HandleAsync_With_NonExistent_Product_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var handler = new TransitionProductStatusHandler(productRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(
            new TransitionProductStatusCommand { ProductId = Guid.NewGuid(), Status = nameof(ProductStatus.Active) },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_With_Invalid_Status_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = new TransitionProductStatusHandler(productRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(
            new TransitionProductStatusCommand { ProductId = product.Id.Value, Status = "NotARealStatus" },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_With_Invalid_Transition_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        product.TransitionTo(ProductStatus.Archived);
        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = new TransitionProductStatusHandler(productRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        await Assert.ThrowsAsync<InvalidProductStatusTransitionException>(() => handler.HandleAsync(
            new TransitionProductStatusCommand { ProductId = product.Id.Value, Status = nameof(ProductStatus.Active) },
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_When_Concurrent_Writer_Wins_Race_Should_Throw_ConcurrencyConflict_Not_Reinterpret()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var product = CreateProduct();
        await productRepository.AddAsync(product, CancellationToken.None);

        var coordinator = new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository);
        var handlerA = new TransitionProductStatusHandler(productRepository, coordinator);

        // Worker B loads the Draft state before Worker A commits, so its own
        // view of "current status" stays stale even after Worker A writes.
        var staleProductRepository = new StaleReadProductRepository(productRepository);
        var handlerB = new TransitionProductStatusHandler(staleProductRepository, new FakeProductSkuConcurrencyCoordinator(productRepository, skuRepository));

        // Worker B loads its (Draft) view first, before Worker A commits.
        await staleProductRepository.GetByIdAsync(product.Id, CancellationToken.None);

        // Both "workers" load the same Draft state independently.
        // Worker A commits Draft -> Active first.
        await handlerA.HandleAsync(
            new TransitionProductStatusCommand { ProductId = product.Id.Value, Status = nameof(ProductStatus.Active) },
            CancellationToken.None);

        // Worker B's own conditional write (still expecting Draft) must lose
        // the race and fail explicitly, never reload and reinterpret its
        // "Active" target against the new Active state as a no-op success.
        await Assert.ThrowsAsync<ProductStatusConcurrencyConflictException>(() => handlerB.HandleAsync(
            new TransitionProductStatusCommand { ProductId = product.Id.Value, Status = nameof(ProductStatus.Active) },
            CancellationToken.None));
    }
}

/// <summary>
/// Test-only decorator that always returns the Product state as it was on
/// its first read, simulating a worker that loaded its view before a
/// concurrent writer committed a change. Write operations are delegated to
/// the shared underlying repository so both "workers" observe the same
/// persisted state.
/// </summary>
internal sealed class StaleReadProductRepository : IProductRepository
{
    private readonly IProductRepository _inner;
    private Product? _staleSnapshot;

    public StaleReadProductRepository(IProductRepository inner)
    {
        _inner = inner;
    }

    public async Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        if (_staleSnapshot is not null)
        {
            return _staleSnapshot;
        }

        _staleSnapshot = await _inner.GetByIdAsync(id, cancellationToken);
        return _staleSnapshot;
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken) => _inner.AddAsync(product, cancellationToken);

    public Task<bool> UpdateStatusAsync(ProductId id, ProductStatus expectedCurrentStatus, ProductStatus newStatus, CancellationToken cancellationToken)
        => _inner.UpdateStatusAsync(id, expectedCurrentStatus, newStatus, cancellationToken);

    public Task<bool> ExistsByCanonicalTaxonomyNodeIdAsync(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId, CancellationToken cancellationToken)
        => _inner.ExistsByCanonicalTaxonomyNodeIdAsync(canonicalTaxonomyNodeId, cancellationToken);

    public Task<bool> ExistsByBrandIdAsync(Yunu.Commerce.Catalog.Domain.Brands.BrandId brandId, CancellationToken cancellationToken)
        => _inner.ExistsByBrandIdAsync(brandId, cancellationToken);

    public Task<bool> ExistsBySegmentDefinitionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId, CancellationToken cancellationToken)
        => _inner.ExistsBySegmentDefinitionIdAsync(segmentDefinitionId, cancellationToken);

    public Task<bool> ExistsBySegmentOptionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId, CancellationToken cancellationToken)
        => _inner.ExistsBySegmentOptionIdAsync(segmentOptionId, cancellationToken);
}
