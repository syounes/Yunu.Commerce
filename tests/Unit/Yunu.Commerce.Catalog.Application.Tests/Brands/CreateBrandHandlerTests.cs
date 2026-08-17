using Yunu.Commerce.Catalog.Application.Brands.CreateBrand;
using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests.Brands;

public class CreateBrandHandlerTests
{
    [Fact]
    public async Task Create_valid_brand_returns_brandid()
    {
        var repo = new FakeBrandRepository();
        var handler = new CreateBrandHandler(repo);

        var command = new CreateBrandCommand { Code = "YUNU", Name = "YUNU" };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.BrandId);
        var created = await repo.GetByIdAsync(new BrandId(result.BrandId), CancellationToken.None);
        Assert.NotNull(created);
        Assert.Equal("YUNU", created!.Code.Value);
    }

    [Fact]
    public async Task Create_duplicate_code_throws()
    {
        var repo = new FakeBrandRepository();
        var existing = Brand.Create(BrandId.New(), new BrandCode("YUNU"), new BrandName("YUNU"));
        await repo.AddAsync(existing, CancellationToken.None);

        var handler = new CreateBrandHandler(repo);
        var command = new CreateBrandCommand { Code = "YUNU", Name = "YUNU" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command, CancellationToken.None));
    }
}
