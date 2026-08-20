namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

/// <summary>
/// Read-side port that resolves the raw candidate rows needed to compute the
/// Effective Segment Definitions of a Canonical Taxonomy node: for the
/// queried node and each of its ancestors, every directly-associated
/// SegmentDefinition (Catalog.CanonicalTaxonomyNodeSegmentDefinitions x
/// Catalog.SegmentDefinitions), regardless of association/definition status
/// (docs task: "Effective Segment Definitions por Canonical Taxonomy Node").
///
/// Filtering (Approved/Active, AppliesToDescendants, precedence,
/// deduplication) is intentionally NOT done here: it belongs to
/// <see cref="EffectiveSegmentDefinitionResolver"/> so the resolution logic
/// stays deterministic, side-effect free and unit-testable without SQL
/// Server. Distinct from <c>ISegmentCatalogRepository</c>, which serves
/// Segment-only read queries unrelated to Canonical Taxonomy ancestry, and
/// from <c>ICanonicalTaxonomyRepository</c>/
/// <c>ISegmentDefinitionRepository</c>, which are Domain Aggregate ports.
/// </summary>
public interface ICanonicalTaxonomySegmentAssociationReader
{
    /// <summary>
    /// Returns one row per (ancestor-or-self node, directly associated
    /// SegmentDefinition) pair for the given node's full ancestor chain
    /// (including the node itself). Returns an empty collection when the
    /// node does not exist or has no associations anywhere in its chain.
    /// </summary>
    Task<IReadOnlyCollection<CanonicalTaxonomySegmentAssociationCandidate>> GetAssociationCandidatesAsync(
        long canonicalTaxonomyNodeId,
        CancellationToken cancellationToken);
}
