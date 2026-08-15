namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Input for one explicit, structured attribute assignment supplied by the
/// caller when creating a Sku (docs task: "SKU attribute foundation"). This
/// stage does not interpret natural-language phrases: the caller must send
/// explicit attribute codes and values. Exactly one of <see cref="Value"/> or
/// <see cref="OptionCode"/> is expected, depending on the resolved Attribute
/// Definition's DataType (OptionCode only for Enum attributes).
/// </summary>
public sealed class SkuAttributeInput
{
    public required string Code { get; init; }

    public int Sequence { get; init; } = 1;

    public string? Value { get; init; }

    public string? OptionCode { get; init; }
}
