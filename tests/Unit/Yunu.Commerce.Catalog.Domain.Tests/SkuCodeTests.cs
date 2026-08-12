using Yunu.Commerce.Catalog.Domain.Products.Skus;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SkuCodeTests
{
    [Fact]
    public void Create_With_Valid_Text_Should_Trim_Whitespace()
    {
        var code = new SkuCode("  ABC-123  ");

        Assert.Equal("ABC-123", code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Invalid_Text_Should_Throw(string? value)
    {
        Assert.Throws<ArgumentException>(() => new SkuCode(value!));
    }

    [Fact]
    public void Instances_With_Same_Trimmed_Value_Should_Be_Equal()
    {
        var first = new SkuCode("ABC-123");
        var second = new SkuCode("  ABC-123  ");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Instances_With_Different_Case_Should_Not_Be_Equal()
    {
        var first = new SkuCode("abc-123");
        var second = new SkuCode("ABC-123");

        Assert.NotEqual(first, second);
    }
}
