namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.CreateCanonicalTaxonomyNode;

/// <summary>
/// Input for creating a Canonical Taxonomy node, root or child (docs task:
/// "Canonical Taxonomy + Segments Domain" §21). ParentId is nullable for a
/// root node. Depth/Path/NormalizedName/SegmentDefinitionId are never
/// accepted from the caller as authority: Application computes/resolves them.
/// </summary>
public sealed class CreateCanonicalTaxonomyNodeCommand
{
    public long? ParentId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? SegmentCode { get; init; }
}
