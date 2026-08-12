using Yunu.Commerce.Catalog.Domain.Products;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class ProductNameTests
{
    [Fact]
    public void Create_With_Valid_Text_Should_Trim_Whitespace()
    {
        var name = new ProductName("  Apple iPhone 17 Pro  ");

        Assert.Equal("Apple iPhone 17 Pro", name.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Invalid_Text_Should_Throw(string? value)
    {
        Assert.Throws<ArgumentException>(() => new ProductName(value!));
    }

    [Fact]
    public void Instances_With_Same_Trimmed_Value_Should_Be_Equal()
    {
        var first = new ProductName("Apple iPhone 17 Pro");
        var second = new ProductName("  Apple iPhone 17 Pro  ");

        Assert.Equal(first, second);
    }
}
