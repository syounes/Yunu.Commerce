using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyRoots;

/// <summary>
/// Orchestrates retrieval of the root Canonical Taxonomy nodes (docs task:
/// "CQRS de leitura e endpoints GET para Segments e Canonical Taxonomy" §3).
/// A root is a node with ParentId = null; no descendants are included.
/// </summary>
public sealed class GetCanonicalTaxonomyRootsHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;

    public GetCanonicalTaxonomyRootsHandler(ICanonicalTaxonomyRepository canonicalTaxonomyRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
    }

    public async Task<IReadOnlyCollection<CanonicalTaxonomyNodeResponse>> HandleAsync(
        GetCanonicalTaxonomyRootsQuery query,
        CancellationToken cancellationToken)
    {
        var roots = await _canonicalTaxonomyRepository.GetRootsAsync(cancellationToken);

        return roots.Select(CanonicalTaxonomyNodeResponseMapper.ToResponse).ToArray();
    }
}
