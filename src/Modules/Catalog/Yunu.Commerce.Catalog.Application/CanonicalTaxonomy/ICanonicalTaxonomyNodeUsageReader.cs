using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Read-side port that answers whether a <see cref="CanonicalTaxonomyNode"/>
/// still has at least one Approved association in
/// Catalog.CanonicalTaxonomyNodeSegmentDefinitions (docs task:
/// "Yunu.Commerce V9 - Canonical Taxonomy Lifecycle + Usage Guards"),
/// mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentDefinitions.ISegmentDefinitionUsageReader"/>.
///
/// Distinct from <see cref="EffectiveSegmentDefinitions.ICanonicalTaxonomySegmentAssociationReader"/>,
/// which resolves ancestry candidates for a queried node; this port instead
/// answers, for a single node's own direct associations, whether at least
/// one is Approved. Only Approved represents effective consumption:
/// Suggested/Rejected/Inactive do not block Archive.
/// </summary>
public interface ICanonicalTaxonomyNodeUsageReader
{
    Task<bool> HasApprovedSegmentAssociationAsync(
        CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        CancellationToken cancellationToken);
}
