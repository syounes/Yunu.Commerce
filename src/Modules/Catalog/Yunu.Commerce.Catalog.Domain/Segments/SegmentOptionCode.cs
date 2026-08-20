namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Canonical, persisted Segment Option code. Immutable Value Object.
/// Mirrors <see cref="SegmentDefinitionCode"/>'s separation of concerns:
/// only validates already-canonical input (required, trimmed, max length)
/// and never transforms arbitrary human text. Uniqueness of a
/// SegmentOptionCode is scoped to its owning SegmentDefinition
/// (Catalog.SegmentOptions.UQ_SegmentOptions_Definition_Code), not global
/// (docs task: "Implementar Domain + Write-Side de SegmentOption").
/// Rules:
/// - required
/// - trimmed
/// - length: 1..100
/// - immutable after the Aggregate is created
/// </summary>
public sealed record SegmentOptionCode
{
    public const int MaxLength = 100;

    public string Value { get; }

    public SegmentOptionCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SegmentOptionCode is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"SegmentOptionCode cannot exceed {MaxLength} characters.", nameof(value));
        }

        Value = trimmed;
    }

    public override string ToString() => Value;
}
