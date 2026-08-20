using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy.EffectiveSegmentDefinitions;

/// <summary>
/// Test-only fake for ICanonicalTaxonomySegmentAssociationReader. Returns a
/// pre-configured set of candidates per queried node id, mirroring what the
/// SQL Server recursive-CTE reader would produce.
/// </summary>
internal sealed class FakeCanonicalTaxonomySegmentAssociationReader : ICanonicalTaxonomySegmentAssociationReader
{
    private readonly Dictionary<long, IReadOnlyCollection<CanonicalTaxonomySegmentAssociationCandidate>> _candidatesByNodeId = new();

    public void Setup(long canonicalTaxonomyNodeId, IReadOnlyCollection<CanonicalTaxonomySegmentAssociationCandidate> candidates)
    {
        _candidatesByNodeId[canonicalTaxonomyNodeId] = candidates;
    }

    public Task<IReadOnlyCollection<CanonicalTaxonomySegmentAssociationCandidate>> GetAssociationCandidatesAsync(
        long canonicalTaxonomyNodeId,
        CancellationToken cancellationToken)
    {
        var candidates = _candidatesByNodeId.TryGetValue(canonicalTaxonomyNodeId, out var found)
            ? found
            : Array.Empty<CanonicalTaxonomySegmentAssociationCandidate>();

        return Task.FromResult(candidates);
    }
}
