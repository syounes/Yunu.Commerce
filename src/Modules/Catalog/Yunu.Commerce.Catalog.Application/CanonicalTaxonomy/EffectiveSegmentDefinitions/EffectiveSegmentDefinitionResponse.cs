namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

/// <summary>
/// A single Segment Definition that effectively applies to a queried
/// Canonical Taxonomy node, after resolving direct associations, ancestor
/// inheritance (AppliesToDescendants) and same-Definition precedence (docs
/// task: "Effective Segment Definitions por Canonical Taxonomy Node").
/// </summary>
public sealed record EffectiveSegmentDefinitionResponse
{
    public required long SegmentDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string AssignmentScope { get; init; }

    /// <summary>
    /// Whether this Segment Definition is effectively required for the
    /// queried node, taken from the winning
    /// Catalog.CanonicalTaxonomyNodeSegmentDefinitions.IsRequired association
    /// (the association-level flag), not from
    /// Catalog.SegmentDefinitions.IsRequired (see the audit note in
    /// EffectiveSegmentDefinitionResolver).
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Catalog.CanonicalTaxonomyNodeSegmentDefinitions.Source for the winning
    /// association ("Yunu" or "AI").
    /// </summary>
    public required string AssociationSource { get; init; }

    /// <summary>
    /// True when the winning association was defined directly on the
    /// queried node; false when it was inherited from an ancestor via
    /// AppliesToDescendants = true.
    /// </summary>
    public required bool IsDirect { get; init; }

    /// <summary>
    /// Id of the Canonical Taxonomy node where the winning association was
    /// actually defined: the queried node itself when <see cref="IsDirect"/>
    /// is true, or the ancestor node that owns the effective association
    /// otherwise.
    /// </summary>
    public required long OriginCanonicalTaxonomyNodeId { get; init; }
}
