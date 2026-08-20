using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions;

/// <summary>
/// Read-side port that answers usage questions about a
/// <see cref="SegmentDefinition"/> that cannot be answered by
/// <see cref="Yunu.Commerce.Catalog.Domain.Products.IProductRepository"/> or
/// <see cref="Yunu.Commerce.Catalog.Domain.Skus.ISkuRepository"/> alone
/// (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards de Segments").
///
/// Distinct from
/// <see cref="Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions.ICanonicalTaxonomySegmentAssociationReader"/>,
/// which resolves ancestry candidates for a queried Canonical Taxonomy node;
/// this port instead answers, for a single SegmentDefinition, whether it is
/// still referenced by at least one effective (Approved) Canonical Taxonomy
/// association anywhere in the tree. Only Approved associations represent
/// effective consumption (docs task, "Canonical Taxonomy association" -
/// Suggested/Rejected/Inactive are not effective).
/// </summary>
public interface ISegmentDefinitionUsageReader
{
    Task<bool> HasApprovedCanonicalTaxonomyAssociationAsync(
        SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken);
}
