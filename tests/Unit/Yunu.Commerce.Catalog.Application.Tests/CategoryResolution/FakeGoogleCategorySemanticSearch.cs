using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

/// <summary>
/// Test-only fake for IGoogleCategorySemanticSearch. Candidates are seeded
/// upfront so tests can simulate specific similarity distributions without
/// pgvector.
/// </summary>
internal sealed class FakeGoogleCategorySemanticSearch : IGoogleCategorySemanticSearch
{
    private readonly List<GoogleCategorySemanticCandidate> _candidates = [];

    public int CallCount { get; private set; }

    public void AddCandidate(GoogleCategorySemanticCandidate candidate) => _candidates.Add(candidate);

    public Task<IReadOnlyList<GoogleCategorySemanticCandidate>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        string locale,
        CancellationToken cancellationToken)
    {
        CallCount++;

        var results = _candidates
            .OrderByDescending(c => c.Similarity)
            .Take(topK)
            .ToArray();

        return Task.FromResult<IReadOnlyList<GoogleCategorySemanticCandidate>>(results);
    }
}
