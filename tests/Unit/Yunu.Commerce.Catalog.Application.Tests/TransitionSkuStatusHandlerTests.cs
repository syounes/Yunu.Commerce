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

        var handler = new TransitionSkuStatusHandler(skuRepository, productRepository);

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

        var handler = new TransitionSkuStatusHandler(skuRepository, productRepository);

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

        var handler = new TransitionSkuStatusHandler(skuRepository, productRepository);

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
        var handler = new TransitionSkuStatusHandler(skuRepository, productRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(
            new TransitionSkuStatusCommand { SkuId = Guid.NewGuid(), Status = nameof(SkuStatus.Active) },
            CancellationToken.None));
    }
}
