using Yunu.Commerce.Catalog.Application.Brands;
using Yunu.Commerce.Catalog.Application.Brands.DeleteBrand;
using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests.Brands;

public class DeleteBrandHandlerTests
{
    [Fact]
    public async Task Delete_unused_brand_succeeds()
    {
        var repo = new FakeBrandRepository();
        var productRepo = new FakeProductRepository();
        var brand = Brand.Create(BrandId.New(), new BrandCode("YUNU"), new BrandName("YUNU"));
        await repo.AddAsync(brand, CancellationToken.None);

        var handler = new DeleteBrandHandler(repo, productRepo);
        var command = new DeleteBrandCommand { BrandId = brand.Id.Value };

        await handler.HandleAsync(command, CancellationToken.None);

        var deleted = await repo.GetByIdAsync(brand.Id, CancellationToken.None);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Delete_nonexistent_throws()
    {
        var repo = new FakeBrandRepository();
        var productRepo = new FakeProductRepository();
        var handler = new DeleteBrandHandler(repo, productRepo);
        var command = new DeleteBrandCommand { BrandId = Guid.NewGuid() };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_brand_in_use_throws()
    {
        var repo = new FakeBrandRepository();
        var productRepo = new FakeProductRepository();
        var brand = Brand.Create(BrandId.New(), new BrandCode("YUNU"), new BrandName("YUNU"));
        await repo.AddAsync(brand, CancellationToken.None);
        productRepo.MarkBrandInUse(brand.Id);

        var handler = new DeleteBrandHandler(repo, productRepo);
        var command = new DeleteBrandCommand { BrandId = brand.Id.Value };

        await Assert.ThrowsAsync<BrandInUseException>(() => handler.HandleAsync(command, CancellationToken.None));
    }
}
