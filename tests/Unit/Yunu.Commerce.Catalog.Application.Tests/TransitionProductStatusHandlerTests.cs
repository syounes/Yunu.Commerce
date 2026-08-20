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

        var handler = new TransitionProductStatusHandler(productRepository, skuRepository);

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

        var handler = new TransitionProductStatusHandler(productRepository, skuRepository);

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

        var handler = new TransitionProductStatusHandler(productRepository, skuRepository);

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
        var handler = new TransitionProductStatusHandler(productRepository, skuRepository);

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

        var handler = new TransitionProductStatusHandler(productRepository, skuRepository);

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

        var handler = new TransitionProductStatusHandler(productRepository, skuRepository);

        await Assert.ThrowsAsync<InvalidProductStatusTransitionException>(() => handler.HandleAsync(
            new TransitionProductStatusCommand { ProductId = product.Id.Value, Status = nameof(ProductStatus.Active) },
            CancellationToken.None));
    }
}
