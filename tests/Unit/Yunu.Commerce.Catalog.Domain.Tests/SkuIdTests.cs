using Yunu.Commerce.Catalog.Domain.Skus;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SkuIdTests
{
    [Fact]
    public void Create_With_NonEmpty_Guid_Should_Succeed()
    {
        var guid = Guid.NewGuid();

        var id = new SkuId(guid);

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void Create_With_Empty_Guid_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => new SkuId(Guid.Empty));
    }

    [Fact]
    public void Instances_With_Same_Guid_Should_Be_Equal()
    {
        var guid = Guid.NewGuid();

        var first = new SkuId(guid);
        var second = new SkuId(guid);

        Assert.Equal(first, second);
    }
}
