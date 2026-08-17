using Yunu.Commerce.Catalog.Application.Brands.UpdateBrand;
using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests.Brands;

public class UpdateBrandHandlerTests
{
    [Fact]
    public async Task Update_rename_and_status_persists()
    {
        var repo = new FakeBrandRepository();
        var brand = Brand.Create(BrandId.New(), new BrandCode("YUNU"), new BrandName("YUNU"));
        await repo.AddAsync(brand, CancellationToken.None);

        var handler = new UpdateBrandHandler(repo);
        var command = new UpdateBrandCommand { BrandId = brand.Id.Value, Name = "Yunu International", Status = "Inactive" };

        await handler.HandleAsync(command, CancellationToken.None);

        var updated = await repo.GetByIdAsync(brand.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Yunu International", updated!.Name.Value);
        Assert.Equal("YUNU INTERNATIONAL", updated.NormalizedName);
        Assert.Equal(BrandStatus.Inactive, updated.Status);
    }

    [Fact]
    public async Task Update_nonexistent_throws()
    {
        var repo = new FakeBrandRepository();
        var handler = new UpdateBrandHandler(repo);
        var command = new UpdateBrandCommand { BrandId = Guid.NewGuid(), Name = "X" };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(command, CancellationToken.None));
    }
}
