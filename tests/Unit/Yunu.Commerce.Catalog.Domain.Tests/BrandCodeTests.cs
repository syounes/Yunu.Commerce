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
        Assert.Equal(code, bc.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("yunu")]
    [InlineData("Yunu")]
    [InlineData("yunu brand")]
    [InlineData("YUNU BRAND")]
    [InlineData("YUNU_BRAND")]
    [InlineData("YUNU-BRAND")]
    [InlineData("Y\u00fanu")]
    [InlineData("Y!")]
    public void Invalid_codes_should_throw(string code)
    {
        Assert.Throws<ArgumentException>(() => new BrandCode(code));
    }

    [Fact]
    public void Equality_is_value_based()
    {
        Assert.Equal(new BrandCode("YUNU"), new BrandCode("YUNU"));
    }
}
