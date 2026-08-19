using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyNodeById;

/// <summary>
/// Orchestrates retrieval of a Canonical Taxonomy node by identity (docs
/// task: "Canonical Taxonomy + Segments Domain" §19). Returns null when the
/// node does not exist.
/// </summary>
public sealed class GetCanonicalTaxonomyNodeByIdHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;

    public GetCanonicalTaxonomyNodeByIdHandler(ICanonicalTaxonomyRepository canonicalTaxonomyRepository)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
    }

    public async Task<CanonicalTaxonomyNodeResponse?> HandleAsync(
        GetCanonicalTaxonomyNodeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var id = new CanonicalTaxonomyNodeId(query.CanonicalTaxonomyNodeId);
        var node = await _canonicalTaxonomyRepository.GetByIdAsync(id, cancellationToken);

        return node is null ? null : CanonicalTaxonomyNodeResponseMapper.ToResponse(node);
    }
}
