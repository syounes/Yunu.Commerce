using Yunu.Commerce.Catalog.Application.AttributeResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeResolution;

/// <summary>
/// Test-only fake for IAttributeSemanticSearch. Candidates are seeded per
/// attribute code (definitions) or per parent attribute code (options), so
/// tests can simulate specific similarity distributions without pgvector.
/// </summary>
internal sealed class FakeAttributeSemanticSearch : IAttributeSemanticSearch
{
    private readonly List<SemanticAttributeCandidate> _definitionCandidates = [];
    private readonly Dictionary<string, List<SemanticAttributeOptionCandidate>> _optionCandidatesByAttribute = new(StringComparer.Ordinal);

    public int DefinitionSearchCallCount { get; private set; }
    public int OptionSearchCallCount { get; private set; }

    public void AddDefinitionCandidate(string attributeCode, string name, double similarity) =>
        _definitionCandidates.Add(new SemanticAttributeCandidate(attributeCode, name, similarity));

    public void AddOptionCandidate(string attributeCode, string optionCode, string name, double similarity)
    {
        if (!_optionCandidatesByAttribute.TryGetValue(attributeCode, out var list))
        {
            list = [];
            _optionCandidatesByAttribute[attributeCode] = list;
        }

        list.Add(new SemanticAttributeOptionCandidate(attributeCode, optionCode, name, similarity));
    }

    public Task<IReadOnlyList<SemanticAttributeCandidate>> SearchDefinitionsAsync(
        ReadOnlyMemory<float> embedding,
        int topK,
        string locale,
        CancellationToken cancellationToken)
    {
        DefinitionSearchCallCount++;

        var results = _definitionCandidates
            .OrderByDescending(c => c.Similarity)
            .Take(topK)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SemanticAttributeCandidate>>(results);
    }

    public Task<IReadOnlyList<SemanticAttributeOptionCandidate>> SearchOptionsAsync(
        string attributeCode,
        ReadOnlyMemory<float> embedding,
        int topK,
        string locale,
        CancellationToken cancellationToken)
    {
        OptionSearchCallCount++;

        if (!_optionCandidatesByAttribute.TryGetValue(attributeCode, out var candidates))
        {
            return Task.FromResult<IReadOnlyList<SemanticAttributeOptionCandidate>>([]);
        }

        var results = candidates
            .OrderByDescending(c => c.Similarity)
            .Take(topK)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SemanticAttributeOptionCandidate>>(results);
    }
}
