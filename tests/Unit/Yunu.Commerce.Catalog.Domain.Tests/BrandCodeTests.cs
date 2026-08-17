using Yunu.Commerce.Catalog.Domain.Brands;
using Xunit;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class BrandCodeTests
{
    [Theory]
    [InlineData("YUNU")]
    [InlineData("LG")]
    [InlineData("3M")]
    [InlineData("SAMSUNG")]
    public void Valid_codes_should_create(string code)
    {
        var bc = new BrandCode(code);
        Assert.Equal(code.Replace(" ", string.Empty).ToUpperInvariant(), bc.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("yunu brand")]
    [InlineData("YUNU_BRAND")]
    [InlineData("YUNU-BRAND")]
    [InlineData("Y!")]
    public void Invalid_codes_should_throw(string code)
    {
        Assert.Throws<ArgumentException>(() => new BrandCode(code));
    }
}
