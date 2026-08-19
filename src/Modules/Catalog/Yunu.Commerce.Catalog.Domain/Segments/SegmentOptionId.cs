namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Strongly typed identifier referencing a Segment Option owned by SQL Server
/// (Catalog.SegmentOptions). Catalog.Domain never queries SQL Server
/// directly (docs task: "Canonical Taxonomy + Segments Domain").
/// </summary>
public readonly record struct SegmentOptionId
{
    public long Value { get; }

    public SegmentOptionId(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentException("SegmentOptionId must be greater than zero.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}
