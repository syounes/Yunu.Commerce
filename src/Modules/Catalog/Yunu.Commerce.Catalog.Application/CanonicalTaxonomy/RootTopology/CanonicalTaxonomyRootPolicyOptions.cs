namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Configured Canonical Taxonomy root topology policy for the current
/// catalog/audit profile (docs task: "Root Topology Policy"), bound from
/// "Catalog:CanonicalTaxonomy:RootTopology". Root topology is governance
/// configuration, not a Domain invariant: multiple root nodes
/// (ParentId == null) remain structurally valid for every
/// <see cref="CanonicalTaxonomyRootMode"/>; a different customer deployment
/// may configure <see cref="CanonicalTaxonomyRootMode.MultipleRoots"/>
/// without any Domain or persistence change.
///
/// The current Yunu catalog profile configures:
/// RootMode = SingleRoot, PrimaryRootCode = "catalog", PrimaryRootName = "Catalog".
/// <see cref="PrimaryRootCode"/> is the stable logical root identity (Code is
/// the stable identifier per docs §11); <see cref="PrimaryRootName"/> is
/// display metadata only and must never be used to define identity.
/// </summary>
public sealed class CanonicalTaxonomyRootPolicyOptions
{
    public required CanonicalTaxonomyRootMode RootMode { get; init; }

    /// <summary>
    /// Required when <see cref="RootMode"/> is <see cref="CanonicalTaxonomyRootMode.SingleRoot"/>;
    /// optional (and structurally meaningless) when <see cref="CanonicalTaxonomyRootMode.MultipleRoots"/>.
    /// </summary>
    public string? PrimaryRootCode { get; init; }

    /// <summary>
    /// Display metadata only; not used as identity. Same optionality rule as
    /// <see cref="PrimaryRootCode"/>.
    /// </summary>
    public string? PrimaryRootName { get; init; }
}
