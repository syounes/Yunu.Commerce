using Yunu.Commerce.Catalog.Domain.Products;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class ProductIdTests
{
    [Fact]
    public void Create_With_NonEmpty_Guid_Should_Succeed()
    {
        var guid = Guid.NewGuid();

        var id = new ProductId(guid);

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void Create_With_Empty_Guid_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => new ProductId(Guid.Empty));
    }

    [Fact]
    public void Instances_With_Same_Guid_Should_Be_Equal()
    {
        var guid = Guid.NewGuid();

        var first = new ProductId(guid);
        var second = new ProductId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Instances_With_Different_Guid_Should_Not_Be_Equal()
    {
        var first = new ProductId(Guid.NewGuid());
        var second = new ProductId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
