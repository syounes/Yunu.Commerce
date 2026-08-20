using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy;

/// <summary>
/// Test-only fake for ICanonicalTaxonomyNodeUsageReader.
/// </summary>
internal sealed class FakeCanonicalTaxonomyNodeUsageReader : ICanonicalTaxonomyNodeUsageReader
{
    private readonly HashSet<CanonicalTaxonomyNodeId> _approvedAssociationsInUse = new();

    public Task<bool> HasApprovedSegmentAssociationAsync(
        CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_approvedAssociationsInUse.Contains(canonicalTaxonomyNodeId));
    }

    /// <summary>
    /// Test-only helper to simulate an Approved Segment Definition
    /// association existing for the given Canonical Taxonomy node.
    /// </summary>
    public void MarkApprovedAssociationInUse(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId)
    {
        _approvedAssociationsInUse.Add(canonicalTaxonomyNodeId);
    }
}
