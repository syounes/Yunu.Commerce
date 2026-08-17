using Yunu.Commerce.Catalog.Application.Brands.ResolveBrand;
using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests.Brands;

public class BrandResolverTests
{
    [Fact]
    public async Task Resolve_by_code_then_normalized_name()
    {
        var repo = new FakeBrandRepository();
        var brand = Brand.Create(BrandId.New(), new BrandCode("YUNU"), new BrandName("Yunu"));
        await repo.AddAsync(brand, CancellationToken.None);

        var resolver = new BrandResolver(repo);

        var byCode = await resolver.ResolveAsync("YUNU", CancellationToken.None);
        Assert.NotNull(byCode);
        Assert.Equal(brand.Id.Value, byCode!.Id.Value);

        var byName = await resolver.ResolveAsync("Yunu", CancellationToken.None);
        Assert.NotNull(byName);
        Assert.Equal(brand.Id.Value, byName!.Id.Value);

        var unknown = await resolver.ResolveAsync("NoSuchBrand", CancellationToken.None);
        Assert.Null(unknown);
    }
}
