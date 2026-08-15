using Xunit;
using Yunu.Commerce.Catalog.Domain.Attributes;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SkuAttributeValueTests
{
    [Fact]
    public void ForText_With_Valid_Value_Should_Succeed()
    {
        var value = SkuAttributeValue.ForText("Branco");

        Assert.Equal(SkuAttributeDataType.Text, value.DataType);
        Assert.Equal("Branco", value.Text);
        Assert.Equal("Branco", value.NormalizedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForText_With_Invalid_Value_Should_Throw(string? invalidValue)
    {
        Assert.Throws<ArgumentException>(() => SkuAttributeValue.ForText(invalidValue!));
    }

    [Fact]
    public void ForMoney_With_Valid_Amount_And_Currency_Should_Succeed()
    {
        var value = SkuAttributeValue.ForMoney(199.90m, "brl");

        Assert.Equal(SkuAttributeDataType.Money, value.DataType);
        Assert.Equal(199.90m, value.MoneyAmount);
        Assert.Equal("BRL", value.CurrencyCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BR")]
    [InlineData("BRLX")]
    public void ForMoney_With_Invalid_Currency_Should_Throw(string invalidCurrency)
    {
        Assert.Throws<ArgumentException>(() => SkuAttributeValue.ForMoney(10m, invalidCurrency));
    }

    [Fact]
    public void ForMeasurement_With_Valid_Value_And_Unit_Should_Succeed()
    {
        var value = SkuAttributeValue.ForMeasurement(41m, "BR");

        Assert.Equal(SkuAttributeDataType.Measurement, value.DataType);
        Assert.Equal(41m, value.MeasurementValue);
        Assert.Equal("BR", value.UnitCode);
    }

    [Fact]
    public void ForMeasurement_With_Empty_Unit_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => SkuAttributeValue.ForMeasurement(41m, " "));
    }

    [Fact]
    public void ForEnum_With_Valid_Option_Code_Should_Succeed()
    {
        var value = SkuAttributeValue.ForEnum("MALE");

        Assert.Equal(SkuAttributeDataType.Enum, value.DataType);
        Assert.Equal("MALE", value.EnumOptionCode);
    }

    [Fact]
    public void ForUrl_With_Invalid_Url_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => SkuAttributeValue.ForUrl("not-a-url"));
    }

    [Fact]
    public void ForJson_With_Invalid_Json_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => SkuAttributeValue.ForJson("{not valid json"));
    }

    [Fact]
    public void Equal_Values_Should_Be_Equal_By_Value()
    {
        var first = SkuAttributeValue.ForText("Branco");
        var second = SkuAttributeValue.ForText("Branco");

        Assert.Equal(first, second);
    }
}
