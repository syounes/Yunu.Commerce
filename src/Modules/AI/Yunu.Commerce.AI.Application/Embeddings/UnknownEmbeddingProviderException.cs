namespace Yunu.Commerce.AI.Application.Embeddings;

/// <summary>
/// Raised when a caller explicitly requests an embedding provider that has not
/// been registered (e.g. "google" before a Google adapter is wired up). Never
/// silently falls back to the default provider in this case; the default
/// provider is only used when no provider is requested at all.
/// </summary>
public sealed class UnknownEmbeddingProviderException : Exception
{
    public UnknownEmbeddingProviderException(string providerName)
        : base($"No embedding provider named '{providerName}' is registered.")
    {
    }
}
