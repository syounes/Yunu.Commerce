using Microsoft.Extensions.Options;

namespace Yunu.Commerce.AI.Application.Embeddings;

/// <summary>
/// Resolves the requested (or default) <see cref="IEmbeddingProvider"/> and
/// delegates embedding generation to it (docs/ai/ai-architecture.md §6,
/// "Provider Abstraction"). This is the only orchestration piece the Host
/// depends on; it never references a specific vendor SDK.
/// </summary>
public sealed class EmbeddingOrchestrator
{
    private readonly IReadOnlyDictionary<string, IEmbeddingProvider> _providersByName;
    private readonly EmbeddingOptions _options;

    public EmbeddingOrchestrator(IEnumerable<IEmbeddingProvider> providers, IOptions<EmbeddingOptions> options)
    {
        _providersByName = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    public Task<EmbeddingResult> GenerateAsync(
        string text,
        string? providerName = null,
        CancellationToken cancellationToken = default)
    {
        var requestedExplicitly = !string.IsNullOrWhiteSpace(providerName);
        var resolvedName = requestedExplicitly ? providerName! : _options.DefaultProvider;

        if (!_providersByName.TryGetValue(resolvedName, out var provider))
        {
            throw new UnknownEmbeddingProviderException(resolvedName);
        }

        return provider.GenerateAsync(text, cancellationToken);
    }
}
