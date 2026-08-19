namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Dedicated, enriched read model for a Segment assignment (docs task:
/// "Canonical Taxonomy + Segments Domain" §32). NormalizedName is
/// intentionally not exposed.
/// </summary>
public sealed class SegmentAssignmentResponse
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string AssignmentScope { get; init; }

    public required IReadOnlyCollection<SegmentOptionAssignmentResponse> Options { get; init; }
}

public sealed class SegmentOptionAssignmentResponse
{
    public required string Code { get; init; }

    public required string Name { get; init; }
}
