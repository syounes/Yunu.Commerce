namespace Yunu.Commerce.Catalog.Domain.Attributes;

/// <summary>
/// Strongly typed identifier referencing an Attribute Definition owned by SQL
/// Server (Catalog.AttributeDefinitions). Catalog.Domain never queries SQL
/// Server directly; the Application layer resolves and validates the
/// definition before asking the Sku Aggregate to assign an attribute
/// (docs task: "SKU attribute foundation").
/// </summary>
public readonly record struct AttributeDefinitionId
{
    public int Value { get; }

    public AttributeDefinitionId(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("AttributeDefinitionId must be greater than zero.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
