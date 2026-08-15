using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

/// <summary>
/// Test-only fake for IEmbeddingProvider. Returns a deterministic vector based
/// on the input text length so tests can assert generation happened without
/// depending on Azure OpenAI.
/// </summary>
internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public const string ProviderName = "fake";
    public const string ModelName = "fake-model";

    private readonly int _dimensions;
    private readonly string _modelName;

    public FakeEmbeddingProvider(int dimensions = 1536, string? modelName = null)
    {
        _dimensions = dimensions;
        _modelName = modelName ?? ModelName;
    }

    public string Name => ProviderName;

    public int CallCount { get; private set; }

    public bool ThrowOnGenerate { get; set; }

    public Task<EmbeddingResult> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        CallCount++;

        if (ThrowOnGenerate)
        {
            throw new EmbeddingGenerationException("Simulated Azure OpenAI failure.");
        }

        var vector = new float[_dimensions];
        vector[0] = text.Length;

        return Task.FromResult(new EmbeddingResult(Name, _modelName, vector));
    }
}
