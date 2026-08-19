using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentEmbeddings;

/// <summary>
/// Test-only fake for ISegmentEmbeddingSourceRepository. Exists exclusively
/// inside this test project (docs task: "Implementar sincronização de
/// embeddings de segmentos").
/// </summary>
internal sealed class FakeSegmentEmbeddingSourceRepository : ISegmentEmbeddingSourceRepository
{
    private readonly List<SegmentDefinitionSource> _definitions = new();
    private readonly List<SegmentOptionSource> _options = new();

    public void AddDefinition(SegmentDefinitionSource definition) => _definitions.Add(definition);

    public void AddOption(SegmentOptionSource option) => _options.Add(option);

    public Task<IReadOnlyCollection<SegmentDefinitionSource>> GetActiveDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<SegmentDefinitionSource>>(_definitions.ToArray());
    }

    public Task<IReadOnlyCollection<SegmentOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<SegmentOptionSource>>(_options.ToArray());
    }
}
