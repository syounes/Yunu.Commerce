using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Categories;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class GetProductByIdHandlerTests
{
    [Fact]
    public async Task Handle_With_Existing_Product_Should_Return_Mapped_Response()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();

        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            BrandId.New(),
            CategoryId.New());

        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = new GetProductByIdHandler(productRepository, skuRepository);
        var query = new GetProductByIdQuery { ProductId = product.Id.Value };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(product.Id.Value, response!.ProductId);
        Assert.Equal("Apple iPhone 17 Pro", response.Name);
        Assert.Equal(product.BrandId.Value, response.BrandId);
        Assert.Equal(product.CategoryId.Value, response.CategoryId);
        Assert.Equal(ProductStatus.Draft.ToString(), response.Status);

        var skuResponse = Assert.Single(response.Skus);
        Assert.Equal("256GB-BLACK", skuResponse.Code);
        Assert.Equal(SkuStatus.Draft.ToString(), skuResponse.Status);
    }

    [Fact]
    public async Task Handle_With_NonExistent_Product_Should_Return_Null()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var handler = new GetProductByIdHandler(productRepository, skuRepository);

        var query = new GetProductByIdQuery { ProductId = Guid.NewGuid() };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Null(response);
    }
}
