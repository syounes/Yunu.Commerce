namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Outcome of evaluating an observed collection of Canonical Taxonomy root
/// nodes against the configured <see cref="CanonicalTaxonomyRootPolicyOptions"/>
/// (docs task: "Root Topology Policy"). Deterministic validation/audit
/// behavior only: never mutates taxonomy, never creates/deletes/moves nodes.
/// </summary>
public enum CanonicalTaxonomyRootTopologyAuditOutcome
{
    /// <summary>The observed root topology matches the configured policy.</summary>
    Valid,

    /// <summary>SingleRoot is configured but no root node was observed.</summary>
    NoRootFound,

    /// <summary>SingleRoot is configured but more than one root node was observed.</summary>
    MultipleRootsFoundForSingleRootPolicy,

    /// <summary>
    /// SingleRoot is configured but no observed root's Code matches the
    /// configured <see cref="CanonicalTaxonomyRootPolicyOptions.PrimaryRootCode"/>.
    /// </summary>
    ConfiguredPrimaryRootNotFound
}
