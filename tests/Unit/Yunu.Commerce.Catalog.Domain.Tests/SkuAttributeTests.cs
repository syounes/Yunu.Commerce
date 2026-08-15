using Xunit;
using Yunu.Commerce.Catalog.Domain.Attributes;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SkuAttributeTests
{
    private static AttributeDefinitionId DefinitionId(int value = 14) => new(value);

    [Fact]
    public void Create_With_Valid_Text_Value_Should_Succeed()
    {
        var attribute = SkuAttribute.Create(DefinitionId(), "color", 1, SkuAttributeValue.ForText("Branco"));

        Assert.Equal("color", attribute.AttributeCode);
        Assert.Equal(1, attribute.Sequence);
        Assert.Equal(SkuAttributeDataType.Text, attribute.DataType);
        Assert.Null(attribute.AttributeOptionId);
    }

    [Fact]
    public void Create_With_Enum_Value_And_Resolved_Option_Should_Succeed()
    {
        var attribute = SkuAttribute.Create(
            new AttributeDefinitionId(47),
            "gender",
            1,
            SkuAttributeValue.ForEnum("MALE"),
            new AttributeOptionId(1401));

        Assert.Equal(new AttributeOptionId(1401), attribute.AttributeOptionId);
    }

    [Fact]
    public void Create_With_Money_Value_Should_Succeed()
    {
        var attribute = SkuAttribute.Create(new AttributeDefinitionId(26), "price", 1, SkuAttributeValue.ForMoney(199.90m, "BRL"));

        Assert.Equal(SkuAttributeDataType.Money, attribute.DataType);
        Assert.Equal(199.90m, attribute.Value.MoneyAmount);
        Assert.Equal("BRL", attribute.Value.CurrencyCode);
    }

    [Fact]
    public void Create_With_Measurement_Value_Should_Succeed()
    {
        var attribute = SkuAttribute.Create(new AttributeDefinitionId(15), "size", 1, SkuAttributeValue.ForMeasurement(41m, "BR"));

        Assert.Equal(SkuAttributeDataType.Measurement, attribute.DataType);
        Assert.Equal(41m, attribute.Value.MeasurementValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Empty_AttributeCode_Should_Throw(string? invalidCode)
    {
        Assert.Throws<ArgumentException>(() =>
            SkuAttribute.Create(DefinitionId(), invalidCode!, 1, SkuAttributeValue.ForText("Branco")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_With_Invalid_Sequence_Should_Throw(int invalidSequence)
    {
        Assert.Throws<ArgumentException>(() =>
            SkuAttribute.Create(DefinitionId(), "color", invalidSequence, SkuAttributeValue.ForText("Branco")));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_With_Confidence_Outside_Range_Should_Throw(decimal invalidConfidence)
    {
        Assert.Throws<ArgumentException>(() =>
            SkuAttribute.Create(DefinitionId(), "color", 1, SkuAttributeValue.ForText("Branco"), confidence: invalidConfidence));
    }

    [Fact]
    public void Create_Enum_Value_Without_AttributeOptionId_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() =>
            SkuAttribute.Create(new AttributeDefinitionId(47), "gender", 1, SkuAttributeValue.ForEnum("MALE")));
    }

    [Fact]
    public void Create_NonEnum_Value_With_AttributeOptionId_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() =>
            SkuAttribute.Create(DefinitionId(), "color", 1, SkuAttributeValue.ForText("Branco"), new AttributeOptionId(1)));
    }

    [Fact]
    public void HasSameEffectiveValueAs_With_Same_Value_Should_Be_True()
    {
        var attribute = SkuAttribute.Create(DefinitionId(), "color", 1, SkuAttributeValue.ForText("Branco"));

        Assert.True(attribute.HasSameEffectiveValueAs(SkuAttributeValue.ForText("Branco"), null));
    }

    [Fact]
    public void HasSameEffectiveValueAs_With_Different_Value_Should_Be_False()
    {
        var attribute = SkuAttribute.Create(DefinitionId(), "color", 1, SkuAttributeValue.ForText("Branco"));

        Assert.False(attribute.HasSameEffectiveValueAs(SkuAttributeValue.ForText("Preto"), null));
    }
}
