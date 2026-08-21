using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Default <see cref="ICanonicalTaxonomyRootTopologyAuditor"/> implementation
/// backed by <see cref="CanonicalTaxonomyRootPolicyOptions"/> (docs task:
/// "Root Topology Policy"). Deterministic, side-effect-free evaluation only;
/// this is not an AI auditor and does not reconstruct, move or create
/// taxonomy nodes.
/// </summary>
public sealed class CanonicalTaxonomyRootTopologyAuditor : ICanonicalTaxonomyRootTopologyAuditor
{
    private readonly CanonicalTaxonomyRootPolicyOptions _options;

    public CanonicalTaxonomyRootTopologyAuditor(IOptions<CanonicalTaxonomyRootPolicyOptions> options)
    {
        _options = options.Value;
    }

    public CanonicalTaxonomyRootTopologyAuditResult Audit(IReadOnlyCollection<CanonicalTaxonomyNode> observedRoots)
    {
        if (observedRoots.Any(node => node.ParentId is not null))
        {
            throw new ArgumentException(
                "Audit expects only root nodes (ParentId == null); a node with ParentId set was supplied.",
                nameof(observedRoots));
        }

        if (_options.RootMode == CanonicalTaxonomyRootMode.MultipleRoots)
        {
            // Any number of independent roots is valid; no primary root is
            // structurally required even when PrimaryRootCode is configured.
            return CanonicalTaxonomyRootTopologyAuditResult.Valid(
                $"MultipleRoots topology accepted with {observedRoots.Count} root node(s).");
        }

        // SingleRoot.
        if (observedRoots.Count == 0)
        {
            return CanonicalTaxonomyRootTopologyAuditResult.Invalid(
                CanonicalTaxonomyRootTopologyAuditOutcome.NoRootFound,
                "SingleRoot topology is configured but no root node was found.");
        }

        if (observedRoots.Count > 1)
        {
            return CanonicalTaxonomyRootTopologyAuditResult.Invalid(
                CanonicalTaxonomyRootTopologyAuditOutcome.MultipleRootsFoundForSingleRootPolicy,
                $"SingleRoot topology is configured but {observedRoots.Count} root nodes were found.");
        }

        var onlyRoot = observedRoots.Single();

        if (!string.Equals(onlyRoot.Code, _options.PrimaryRootCode, StringComparison.Ordinal))
        {
            return CanonicalTaxonomyRootTopologyAuditResult.Invalid(
                CanonicalTaxonomyRootTopologyAuditOutcome.ConfiguredPrimaryRootNotFound,
                $"SingleRoot topology is configured with PrimaryRootCode '{_options.PrimaryRootCode}', but the observed root has Code '{onlyRoot.Code}'.");
        }

        return CanonicalTaxonomyRootTopologyAuditResult.Valid(
            $"SingleRoot topology valid: the configured primary root '{_options.PrimaryRootCode}' matches the observed root.");
    }
}
