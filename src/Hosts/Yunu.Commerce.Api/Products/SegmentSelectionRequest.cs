namespace Yunu.Commerce.Api.Products;

/// <summary>
/// HTTP request contract for one explicit Segment selection (docs task:
/// "Canonical Taxonomy + Segments Domain" §26). The caller supplies only the
/// Segment's stable Code and the selected OptionCodes.
/// </summary>
public sealed class SegmentSelectionRequest
{
    public required string Code { get; init; }

    public IReadOnlyCollection<string> OptionCodes { get; init; } = Array.Empty<string>();
}
