using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Explicit, hand-written mapping between the CanonicalTaxonomyNode Aggregate
/// and its Application read model (docs/adr/0001 §9, "prefer explicit
/// mapping"). NormalizedName is intentionally not exposed in the public
/// contract (docs task: "Canonical Taxonomy + Segments Domain" §32-§33).
/// </summary>
internal static class CanonicalTaxonomyNodeResponseMapper
{
    public static CanonicalTaxonomyNodeResponse ToResponse(CanonicalTaxonomyNode node)
    {
        return new CanonicalTaxonomyNodeResponse
        {
            CanonicalTaxonomyNodeId = node.Id.Value,
            ParentId = node.ParentId?.Value,
            GoogleCategoryId = node.GoogleCategoryId,
            Code = node.Code,
            Name = node.Name,
            Description = node.Description,
            Depth = node.Depth,
            Path = node.Path,
            Source = node.Source.ToString(),
            Status = node.Status.ToString()
        };
    }
}
