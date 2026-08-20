namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

/// <summary>
/// Raw candidate row combining a Canonical Taxonomy node (the queried node or
/// one of its ancestors) with one of its directly-associated Segment
/// Definitions (Catalog.CanonicalTaxonomyNodeSegmentDefinitions x
/// Catalog.SegmentDefinitions). Intentionally unfiltered: all business rules
/// (Approved/Active status, AppliesToDescendants, precedence, deduplication)
/// are applied deterministically by
/// <see cref="EffectiveSegmentDefinitionResolver"/> in the Application layer,
/// not in SQL, so the resolution logic is unit-testable without a database
/// (docs task: "Effective Segment Definitions por Canonical Taxonomy Node").
/// </summary>
public sealed record CanonicalTaxonomySegmentAssociationCandidate
{
    /// <summary>
    /// Id of the node (the queried node itself, or one of its ancestors)
    /// that owns this association.
    /// </summary>
    public required long OriginCanonicalTaxonomyNodeId { get; init; }

    /// <summary>
    /// Depth of <see cref="OriginCanonicalTaxonomyNodeId"/> in the Canonical
    /// Taxonomy tree. Used to rank precedence: a deeper origin node is more
    /// specific and wins over a shallower one for the same SegmentDefinition.
    /// </summary>
    public required int OriginNodeDepth { get; init; }

    /// <summary>
    /// True when <see cref="OriginCanonicalTaxonomyNodeId"/> is the node that
    /// was originally queried (a direct association); false when it is one
    /// of its ancestors (a candidate for inheritance).
    /// </summary>
    public required bool IsSelf { get; init; }

    public required bool AppliesToDescendants { get; init; }

    /// <summary>
    /// Catalog.CanonicalTaxonomyNodeSegmentDefinitions.Status: Suggested,
    /// Approved, Rejected or Inactive. Only Approved associations are
    /// consumable by the effective catalog resolution.
    /// </summary>
    public required string AssociationStatus { get; init; }

    /// <summary>
    /// Catalog.CanonicalTaxonomyNodeSegmentDefinitions.Source: Yunu or AI.
    /// </summary>
    public required string AssociationSource { get; init; }

    /// <summary>
    /// Catalog.CanonicalTaxonomyNodeSegmentDefinitions.IsRequired: whether
    /// this specific node-to-definition association is required. This is the
    /// value the effective resolution surfaces (see
    /// EffectiveSegmentDefinitionResolver remarks on the separate,
    /// non-contextual SegmentDefinitions.IsRequired column).
    /// </summary>
    public required bool AssociationIsRequired { get; init; }

    public required long SegmentDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Catalog.SegmentDefinitions.Status: Draft, Active, Inactive or
    /// Archived. Only Active definitions are consumable.
    /// </summary>
    public required string DefinitionStatus { get; init; }

    public required string AssignmentScope { get; init; }
}
