using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

/// <summary>
/// Test-only fake for IAttributeEmbeddingSourceRepository. Exists exclusively
/// inside this test project (docs task: "SKU attribute embedding
/// synchronization pipeline").
/// </summary>
internal sealed class FakeAttributeEmbeddingSourceRepository : IAttributeEmbeddingSourceRepository
{
    private readonly List<AttributeDefinitionSource> _definitions = new();
    private readonly List<AttributeOptionSource> _options = new();

    public void AddDefinition(AttributeDefinitionSource definition) => _definitions.Add(definition);

    public void AddOption(AttributeOptionSource option) => _options.Add(option);

    public Task<IReadOnlyCollection<AttributeDefinitionSource>> GetActiveSearchableDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<AttributeDefinitionSource>>(_definitions.ToArray());
    }

    public Task<IReadOnlyCollection<AttributeOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<AttributeOptionSource>>(_options.ToArray());
    }
}
