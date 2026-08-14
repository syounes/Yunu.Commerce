namespace Yunu.Commerce.AI.Application.Embeddings;

/// <summary>
/// Provider-agnostic embedding orchestration configuration, bound from the
/// "AI:Embeddings" configuration section. Contains only concepts that make
/// sense regardless of which provider is used; provider-specific settings
/// (endpoint, API key, deployment name, ...) live in Infrastructure under
/// "AI:Embeddings:Providers:{ProviderName}" instead.
/// </summary>
public sealed class EmbeddingOptions
{
    public required string DefaultProvider { get; init; }
}
