namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.UpdateCanonicalTaxonomyNode;

public sealed class UpdateCanonicalTaxonomyNodeCommand
{
    public required long CanonicalTaxonomyNodeId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// Optional lifecycle transition (docs task: "Yunu.Commerce V9 -
    /// Canonical Taxonomy Lifecycle + Usage Guards"). When null, the node's
    /// current Status is preserved and this call behaves exactly as a
    /// rename (backwards compatible with the pre-V9 Update contract). When
    /// supplied, must be one of Draft/Active/Inactive/Archived and follow
    /// <see cref="Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy.CanonicalTaxonomyNode.TransitionTo"/>'s
    /// allowed transitions.
    /// </summary>
    public string? Status { get; init; }
}
