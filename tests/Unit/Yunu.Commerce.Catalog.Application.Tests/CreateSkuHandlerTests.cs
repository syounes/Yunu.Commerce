using Xunit;
using Yunu.Commerce.Catalog.Application.Skus.CreateSku;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Families;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class CreateSkuHandlerTests
{
    private static GoogleCategoryReference CreateGoogleCategory()
    {
        return new GoogleCategoryReference(1234, "Apparel & Accessories > Shoes > Athletic Shoes");
    }

    [Fact]
    public async Task Handle_With_Valid_ProductId_Should_Create_Sku()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();

        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            description: null,
            new BrandId(Guid.NewGuid()),
            new FamilyId(Guid.NewGuid()),
            CreateGoogleCategory());

        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = new CreateSkuHandler(productRepository, skuRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = "256GB-BLACK",
            Gtin = "1234567890123"
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.SkuId);
        Assert.Equal(1, skuRepository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_NonExistent_ProductId_Should_Throw()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var handler = new CreateSkuHandler(productRepository, skuRepository);

        var command = new CreateSkuCommand
        {
            ProductId = Guid.NewGuid(),
            Code = "256GB-BLACK",
            Gtin = null
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(command, CancellationToken.None));

        Assert.Contains("does not exist", exception.Message);
        Assert.Equal(0, skuRepository.AddAsyncCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_With_Invalid_Code_Should_Throw(string? invalidCode)
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();

        var product = Product.Create(
            ProductId.New(),
            new ProductName("Test Product"),
            description: null,
            new BrandId(Guid.NewGuid()),
            new FamilyId(Guid.NewGuid()),
            CreateGoogleCategory());

        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = new CreateSkuHandler(productRepository, skuRepository);

        var command = new CreateSkuCommand
        {
            ProductId = product.Id.Value,
            Code = invalidCode!,
            Gtin = null
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }
}
