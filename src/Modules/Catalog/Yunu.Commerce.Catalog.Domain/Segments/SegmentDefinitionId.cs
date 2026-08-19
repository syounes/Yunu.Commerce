namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Strongly typed identifier referencing a Segment Definition owned by SQL
/// Server (Catalog.SegmentDefinitions). Catalog.Domain never queries SQL
/// Server directly; the Application layer resolves and validates the
/// definition before a Product or Sku assigns it (docs task: "Canonical
/// Taxonomy + Segments Domain").
/// </summary>
public readonly record struct SegmentDefinitionId
{
    public long Value { get; }

    public SegmentDefinitionId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("SegmentDefinitionId must be greater than zero.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
