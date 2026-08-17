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
}
