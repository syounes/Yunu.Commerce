namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Human-display Segment Option name Value Object. Non-null, non-empty,
/// non-whitespace, trimmed, with a documented maximum length matching the
/// SQL Server column (Catalog.SegmentOptions.Name NVARCHAR(200)). Mirrors
/// <see cref="SegmentDefinitionName"/> (docs task: "Implementar Domain +
/// Write-Side de SegmentOption").
/// </summary>
public sealed record SegmentOptionName
{
    public const int MaxLength = 200;

    public string Value { get; }

    public SegmentOptionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SegmentOptionName is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"SegmentOptionName cannot exceed {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public override string ToString() => Value;
}
