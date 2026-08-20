using Xunit;
using Yunu.Commerce.Catalog.Application.Skus.GetSkuById;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class GetSkuByIdHandlerTests
{
    [Fact]
    public async Task Handle_With_Existing_Sku_Should_Return_Mapped_Response()
    {
        var repository = new FakeSkuRepository();

        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("256GB-BLACK"), "0000000000001");
        await repository.AddAsync(sku, CancellationToken.None);

        var handler = new GetSkuByIdHandler(repository, new FakeProductRepository());
        var query = new GetSkuByIdQuery { SkuId = sku.Id.Value };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(sku.Id.Value, response!.SkuId);
        Assert.Equal(sku.ProductId.Value, response.ProductId);
        Assert.Equal("256GB-BLACK", response.Code);
        Assert.Equal("0000000000001", response.Gtin);
        Assert.Equal(SkuStatus.Draft.ToString(), response.Status);
    }

    [Fact]
    public async Task Handle_With_NonExistent_Sku_Should_Return_Null()
    {
        var repository = new FakeSkuRepository();
        var handler = new GetSkuByIdHandler(repository, new FakeProductRepository());

        var query = new GetSkuByIdQuery { SkuId = Guid.NewGuid() };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Null(response);
    }
}
