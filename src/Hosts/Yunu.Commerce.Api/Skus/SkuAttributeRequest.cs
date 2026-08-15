namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// HTTP request contract for one explicit, structured Sku attribute
/// assignment (docs task: "SKU attribute foundation"). Exactly one of
/// <see cref="Value"/> or <see cref="OptionCode"/> is expected, depending on
/// the resolved Attribute Definition's DataType (OptionCode only for Enum
/// attributes).
/// </summary>
public sealed class SkuAttributeRequest
{
    public required string Code { get; init; }

    public int Sequence { get; init; } = 1;

    public string? Value { get; init; }

    public string? OptionCode { get; init; }
}
