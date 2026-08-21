using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Evaluates an observed collection of Canonical Taxonomy root nodes
/// (ParentId == null) against the configured Root Topology Policy (docs
/// task: "Root Topology Policy"). Pure validation/audit: never mutates
/// taxonomy, never creates/reassigns/deletes roots.
/// </summary>
public interface ICanonicalTaxonomyRootTopologyAuditor
{
    CanonicalTaxonomyRootTopologyAuditResult Audit(IReadOnlyCollection<CanonicalTaxonomyNode> observedRoots);
}
