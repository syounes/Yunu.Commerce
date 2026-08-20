namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Canonical, persisted Segment Definition code. Immutable Value Object.
/// SegmentDefinitionCode is already-canonical input: the Value Object only
/// validates it (required, trimmed, max length) and never transforms
/// arbitrary human text (no accent stripping, no implicit uppercasing),
/// mirroring <see cref="Yunu.Commerce.Catalog.Domain.Brands.BrandCode"/>'s
/// separation of concerns. This preserves existing codes such as
/// "gender", "target_audience" and "sport_modality".
/// Rules:
/// - required
/// - trimmed
/// - length: 1..100
/// - immutable after the Aggregate is created
/// </summary>
public sealed record SegmentDefinitionCode
{
    public const int MaxLength = 100;

    public string Value { get; }

    public SegmentDefinitionCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SegmentDefinitionCode is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"SegmentDefinitionCode cannot exceed {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public override string ToString() => Value;
}
