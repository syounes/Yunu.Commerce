namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Human-display Segment Definition name Value Object. Non-null, non-empty,
/// non-whitespace, trimmed, with a documented maximum length matching the
/// SQL Server column (Catalog.SegmentDefinitions.Name NVARCHAR(200)).
/// </summary>
public sealed record SegmentDefinitionName
{
    public const int MaxLength = 200;

    public string Value { get; }

    public SegmentDefinitionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SegmentDefinitionName is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"SegmentDefinitionName cannot exceed {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public override string ToString() => Value;
}
