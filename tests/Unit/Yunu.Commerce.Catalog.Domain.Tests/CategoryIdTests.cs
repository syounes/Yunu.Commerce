using Yunu.Commerce.Catalog.Domain.Categories;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class CategoryIdTests
{
    [Fact]
    public void Create_With_NonEmpty_Guid_Should_Succeed()
    {
        var guid = Guid.NewGuid();

        var id = new CategoryId(guid);

        Assert.Equal(guid, id.Value);
    }

    [Fact]
    public void Create_With_Empty_Guid_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => new CategoryId(Guid.Empty));
    }
}
