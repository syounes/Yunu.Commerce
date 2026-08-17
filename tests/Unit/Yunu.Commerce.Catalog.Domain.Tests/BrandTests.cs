using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Brands.Events;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class BrandTests
{
    [Fact]
    public void Create_should_set_properties_and_raise_event()
    {
        var id = BrandId.New();
        var code = new BrandCode("YUNU");
        var name = new BrandName("YUNU");

        var brand = Brand.Create(id, code, name);

        Assert.Equal(id, brand.Id);
        Assert.Equal(code.Value, brand.Code.Value);
        Assert.Equal(name.Value, brand.Name.Value);
        Assert.Equal("YUNU", brand.NormalizedName);
        Assert.Equal(BrandStatus.Active, brand.Status);
        Assert.NotNull(brand.DomainEvents);
        Assert.Contains(brand.DomainEvents, e => e is BrandCreatedDomainEvent);
    }

    [Fact]
    public void Reconstitute_should_not_raise_domain_events()
    {
        var brand = Brand.Reconstitute(
            BrandId.New(),
            new BrandCode("SAMSUNG"),
            new BrandName("Samsung"),
            "SAMSUNG",
            BrandStatus.Active,
            DateTimeOffset.UtcNow);

        Assert.Empty(brand.DomainEvents);
    }

    [Fact]
    public void Rename_should_update_name_and_normalized_name()
    {
        var brand = Brand.Create(BrandId.New(), new BrandCode("HP"), new BrandName("Hewlett Packard"));

        brand.Rename(new BrandName("Hewlett-Packard"));

        Assert.Equal("Hewlett-Packard", brand.Name.Value);
        Assert.Equal(Brand.ComputeNormalizedName("Hewlett-Packard"), brand.NormalizedName);
    }

    [Fact]
    public void Rename_to_same_effective_name_is_idempotent()
    {
        var brand = Brand.Create(BrandId.New(), new BrandCode("HP"), new BrandName("Hewlett Packard"));
        var normalizedBefore = brand.NormalizedName;

        brand.Rename(new BrandName("Hewlett Packard"));

        Assert.Equal(normalizedBefore, brand.NormalizedName);
    }

    [Fact]
    public void BrandCode_cannot_change_after_creation()
    {
        var brand = Brand.Create(BrandId.New(), new BrandCode("HP"), new BrandName("Hewlett Packard"));

        Assert.Equal("HP", brand.Code.Value);
        // Code has no setter/mutation method: compile-time immutability guarantee.
    }

    [Fact]
    public void Activate_and_Deactivate_are_idempotent()
    {
        var brand = Brand.Create(BrandId.New(), new BrandCode("HP"), new BrandName("Hewlett Packard"));

        brand.Deactivate();
        brand.Deactivate();
        Assert.Equal(BrandStatus.Inactive, brand.Status);

        brand.Activate();
        brand.Activate();
        Assert.Equal(BrandStatus.Active, brand.Status);
    }
}
