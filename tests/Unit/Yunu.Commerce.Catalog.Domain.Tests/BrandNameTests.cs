using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class BrandNameTests
{
    [Fact]
    public void Null_or_whitespace_name_should_throw()
    {
        Assert.Throws<ArgumentException>(() => new BrandName(null!));
        Assert.Throws<ArgumentException>(() => new BrandName(""));
        Assert.Throws<ArgumentException>(() => new BrandName("   "));
    }

    [Fact]
    public void Trimmed_name_is_preserved()
    {
        var n = new BrandName("  Hewlett Packard  ");
        Assert.Equal("Hewlett Packard", n.Value);
    }
}
