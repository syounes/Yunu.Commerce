namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Dedicated read model for a Canonical Taxonomy node, decoupled from the
/// Domain Aggregate (docs task: "Canonical Taxonomy + Segments Domain" §19).
/// </summary>
public sealed class CanonicalTaxonomyNodeResponse
{
    public required long CanonicalTaxonomyNodeId { get; init; }

    public long? ParentId { get; init; }

    public long? GoogleCategoryId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required int Depth { get; init; }

    public required string Path { get; init; }

    public required string Source { get; init; }

    public required string Status { get; init; }
}
