using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

/// <summary>
/// Orchestrates resolution of the Effective Segment Definitions of a
/// Canonical Taxonomy node (docs task: "Effective Segment Definitions por
/// Canonical Taxonomy Node"). Read-only; does not mutate any Aggregate.
/// Returns an empty result (never throws) both when the node has no
/// associations and when the node itself does not exist, mirroring the
/// existing GetSegmentDefinitionById "return null/empty for absence" style
/// used elsewhere in this read side.
/// </summary>
public sealed class GetEffectiveSegmentDefinitionsHandler
{
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly ICanonicalTaxonomySegmentAssociationReader _associationReader;

    public GetEffectiveSegmentDefinitionsHandler(
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        ICanonicalTaxonomySegmentAssociationReader associationReader)
    {
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _associationReader = associationReader;
    }

    public async Task<IReadOnlyCollection<EffectiveSegmentDefinitionResponse>> HandleAsync(
        GetEffectiveSegmentDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        var nodeId = new CanonicalTaxonomyNodeId(query.CanonicalTaxonomyNodeId);

        var node = await _canonicalTaxonomyRepository.GetByIdAsync(nodeId, cancellationToken);
        if (node is null)
        {
            return Array.Empty<EffectiveSegmentDefinitionResponse>();
        }

        var candidates = await _associationReader.GetAssociationCandidatesAsync(query.CanonicalTaxonomyNodeId, cancellationToken);

        return EffectiveSegmentDefinitionResolver.Resolve(candidates);
    }
}
