namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.UpdateCanonicalTaxonomyNode;

public sealed class UpdateCanonicalTaxonomyNodeCommand
{
    public required long CanonicalTaxonomyNodeId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}
