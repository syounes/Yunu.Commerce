using Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Skus;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class GetSkusByProductIdHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Only_Skus_For_Requested_Product()
    {
        var repository = new FakeSkuRepository();

        var productId = ProductId.New();
        var otherProductId = ProductId.New();

        var sku1 = Sku.Create(SkuId.New(), productId, new SkuCode("256GB-BLACK"));
        var sku2 = Sku.Create(SkuId.New(), productId, new SkuCode("512GB-BLACK"));
        var otherSku = Sku.Create(SkuId.New(), otherProductId, new SkuCode("OTHER-SKU"));

        await repository.AddAsync(sku1, CancellationToken.None);
        await repository.AddAsync(sku2, CancellationToken.None);
        await repository.AddAsync(otherSku, CancellationToken.None);

        var handler = new GetSkusByProductIdHandler(repository);
        var query = new GetSkusByProductIdQuery { ProductId = productId.Value };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Equal(2, response.Count);
        Assert.All(response, sku => Assert.Equal(productId.Value, sku.ProductId));
    }

    [Fact]
    public async Task Handle_With_No_Matching_Skus_Should_Return_Empty_Collection()
    {
        var repository = new FakeSkuRepository();
        var handler = new GetSkusByProductIdHandler(repository);

        var query = new GetSkusByProductIdQuery { ProductId = Guid.NewGuid() };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Empty(response);
    }
}
