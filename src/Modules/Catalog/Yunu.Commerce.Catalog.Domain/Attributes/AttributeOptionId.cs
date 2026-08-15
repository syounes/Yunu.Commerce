namespace Yunu.Commerce.Catalog.Domain.Attributes;

/// <summary>
/// Strongly typed identifier referencing an Attribute Option owned by SQL
/// Server (Catalog.AttributeOptions). Only used when the owning Attribute
/// Definition's DataType is Enum; resolved and validated by the Application
/// layer before the Sku Aggregate assigns the attribute
/// (docs task: "SKU attribute foundation").
/// </summary>
public readonly record struct AttributeOptionId
{
    public int Value { get; }

    public AttributeOptionId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("AttributeOptionId must be greater than zero.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
