using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyChildren;

/// <summary>
/// Orchestrates retrieval of the direct children of a Canonical Taxonomy
/// node (docs task: "Canonical Taxonomy + Segments Domain" §19).
/// </summary>
public sealed class GetCanonicalTaxonomyChildrenHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;

    public GetCanonicalTaxonomyChildrenHandler(ICanonicalTaxonomyRepository canonicalTaxonomyRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
    }

    public async Task<IReadOnlyCollection<CanonicalTaxonomyNodeResponse>> HandleAsync(
        GetCanonicalTaxonomyChildrenQuery query,
        CancellationToken cancellationToken)
    {
        var parentId = new CanonicalTaxonomyNodeId(query.ParentId);
        var children = await _canonicalTaxonomyRepository.GetChildrenAsync(parentId, cancellationToken);

        return children.Select(CanonicalTaxonomyNodeResponseMapper.ToResponse).ToArray();
    }
}
