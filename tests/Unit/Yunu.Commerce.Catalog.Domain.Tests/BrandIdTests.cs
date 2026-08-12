using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class BrandIdTests
{
    [Fact]
    public void Create_With_NonEmpty_Guid_Should_Succeed()
    {
        var guid = Guid.NewGuid();

        var id = new BrandId(guid);

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void Create_With_Empty_Guid_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => new BrandId(Guid.Empty));
    }
}
