namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// A single selected option within a <see cref="SegmentAssignment"/>. Mirrors
/// the pattern of <see cref="Yunu.Commerce.Catalog.Domain.Attributes.SkuAttribute"/>:
/// keeps the resolved identity (<see cref="SegmentOptionId"/>) plus a stable
/// code, so that Name/NormalizedName/SemanticText (reference-only data owned
/// by SQL Server) never need to be persisted alongside the assignment
/// (docs task: "Canonical Taxonomy + Segments Domain" §11).
/// </summary>
public sealed record SegmentOptionSelection
{
    public SegmentOptionId SegmentOptionId { get; }

    public string OptionCode { get; }

    public SegmentOptionSelection(SegmentOptionId segmentOptionId, string optionCode)
    {
        if (string.IsNullOrWhiteSpace(optionCode))
        {
            throw new ArgumentException("Option code cannot be null, empty or whitespace.", nameof(optionCode));
        }

        SegmentOptionId = segmentOptionId;
        OptionCode = optionCode.Trim();
    }
}
