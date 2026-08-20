using Yunu.Commerce.Catalog.Application.SegmentDefinitions;
using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentDefinitions;

/// <summary>
/// Test-only fake for ISegmentDefinitionUsageReader.
/// </summary>
internal sealed class FakeSegmentDefinitionUsageReader : ISegmentDefinitionUsageReader
{
    private readonly HashSet<SegmentDefinitionId> _approvedAssociationsInUse = new();

    public Task<bool> HasApprovedCanonicalTaxonomyAssociationAsync(
        SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_approvedAssociationsInUse.Contains(segmentDefinitionId));
    }

    /// <summary>
    /// Test-only helper to simulate an Approved Canonical Taxonomy association
    /// existing for the given SegmentDefinition.
    /// </summary>
    public void MarkApprovedAssociationInUse(SegmentDefinitionId segmentDefinitionId)
    {
        _approvedAssociationsInUse.Add(segmentDefinitionId);
    }
}
