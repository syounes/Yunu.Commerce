namespace Yunu.Commerce.Catalog.Application.SegmentCatalog;

/// <summary>
/// Caller input for assigning a Segment to a Product or Sku (docs task:
/// "Canonical Taxonomy + Segments Domain" §26). The caller supplies only the
/// Segment's stable Code and the OptionCodes selected; SegmentDefinitionId,
/// SegmentOptionId, Name, NormalizedName, AssignmentScope, SelectionMode and
/// Status are never accepted from the caller and are resolved/validated by
/// Catalog.Application against SQL Server before the Product/Sku Aggregate is
/// asked to assign the Segment.
/// </summary>
public sealed class SegmentSelectionInput
{
    public required string Code { get; init; }

    public IReadOnlyCollection<string> OptionCodes { get; init; } = Array.Empty<string>();
}
