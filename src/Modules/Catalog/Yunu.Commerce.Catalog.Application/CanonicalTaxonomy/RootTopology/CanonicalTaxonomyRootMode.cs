namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Configured Canonical Taxonomy root topology governance mode (docs task:
/// "Root Topology Policy"). This is catalog/audit governance configuration,
/// not a Domain invariant: <see cref="Domain.CanonicalTaxonomy.CanonicalTaxonomyNode"/>
/// and <see cref="Domain.CanonicalTaxonomy.ICanonicalTaxonomyRepository"/>
/// remain unaware of it and continue to support any number of root nodes
/// (ParentId == null) structurally.
/// </summary>
public enum CanonicalTaxonomyRootMode
{
    /// <summary>
    /// The catalog profile expects exactly one effective root, identified by
    /// <see cref="CanonicalTaxonomyRootPolicyOptions.PrimaryRootCode"/>.
    /// </summary>
    SingleRoot,

    /// <summary>
    /// The catalog profile allows any number of independent root nodes; no
    /// root is required to carry special structural meaning.
    /// </summary>
    MultipleRoots
}
