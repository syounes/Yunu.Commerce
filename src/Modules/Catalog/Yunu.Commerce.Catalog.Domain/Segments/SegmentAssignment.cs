namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// A single Segment assignment belonging to one owner (Product or Sku).
/// Not an Aggregate Root: it only exists inside the owner Aggregate's
/// consistency boundary and is never persisted or referenced independently
/// (docs task: "Canonical Taxonomy + Segments Domain" §11-§12).
///
/// SegmentDefinitionId/SegmentOptionId identities are resolved and validated
/// by Catalog.Application against SQL Server (Catalog.SegmentDefinitions /
/// Catalog.SegmentOptions) before this type is constructed; this type only
/// protects the invariants that do not depend on external reference data:
/// no duplicated options within the same assignment, and a stable
/// SegmentDefinitionId/SegmentCode pairing.
///
/// Name, NormalizedName, SemanticText, AssignmentScope and SelectionMode are
/// intentionally not persisted here; they belong to the SQL Server reference
/// catalog and are enriched only at read time.
/// </summary>
public sealed class SegmentAssignment
{
    private readonly List<SegmentOptionSelection> _options;

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public string SegmentCode { get; }

    public IReadOnlyCollection<SegmentOptionSelection> Options => _options;

    private SegmentAssignment(SegmentDefinitionId segmentDefinitionId, string segmentCode, List<SegmentOptionSelection> options)
    {
        SegmentDefinitionId = segmentDefinitionId;
        SegmentCode = segmentCode;
        _options = options;
    }

    /// <summary>
    /// Creates a validated Segment assignment. At least one option is
    /// required; duplicated <see cref="SegmentOptionId"/>/OptionCode values
    /// within the same assignment are rejected.
    /// </summary>
    public static SegmentAssignment Create(
        SegmentDefinitionId segmentDefinitionId,
        string segmentCode,
        IEnumerable<SegmentOptionSelection> options)
    {
        if (string.IsNullOrWhiteSpace(segmentCode))
        {
            throw new ArgumentException("Segment code cannot be null, empty or whitespace.", nameof(segmentCode));
        }

        var materializedOptions = options.ToList();

        if (materializedOptions.Count == 0)
        {
            throw new ArgumentException("A Segment assignment requires at least one option.", nameof(options));
        }

        if (materializedOptions.Select(o => o.SegmentOptionId).Distinct().Count() != materializedOptions.Count)
        {
            throw new ArgumentException("A Segment assignment cannot contain duplicated options.", nameof(options));
        }

        return new SegmentAssignment(segmentDefinitionId, segmentCode.Trim(), materializedOptions);
    }

    /// <summary>
    /// Rehydrates a Segment assignment from persisted state without
    /// re-running Create's option-based validation ordering concerns; used
    /// exclusively by Infrastructure persistence mappers.
    /// </summary>
    public static SegmentAssignment Hydrate(
        SegmentDefinitionId segmentDefinitionId,
        string segmentCode,
        IEnumerable<SegmentOptionSelection> options)
    {
        return new SegmentAssignment(segmentDefinitionId, segmentCode, options.ToList());
    }

    /// <summary>
    /// Whether this assignment's effective set of options is identical to
    /// the supplied one (docs task: "Assign the same value again must be
    /// idempotent"). Order-independent comparison by SegmentOptionId.
    /// </summary>
    public bool HasSameEffectiveOptionsAs(IEnumerable<SegmentOptionSelection> options)
    {
        var otherIds = options.Select(o => o.SegmentOptionId).ToHashSet();
        var thisIds = _options.Select(o => o.SegmentOptionId).ToHashSet();

        return thisIds.SetEquals(otherIds);
    }
}
