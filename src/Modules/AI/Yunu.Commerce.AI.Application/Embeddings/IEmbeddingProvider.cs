namespace Yunu.Commerce.AI.Application.Embeddings;

/// <summary>
/// Provider-agnostic port for generating a semantic text embedding
/// (docs/ai/ai-architecture.md §6, "Provider Abstraction"). This is the single
/// boundary the Application layer depends on: it must never reference Azure,
/// Google, Ollama, the OpenAI SDK, endpoints or credentials directly.
/// Infrastructure supplies one adapter per vendor (e.g. Azure OpenAI), each
/// identified by <see cref="Name"/> so <see cref="EmbeddingOrchestrator"/> can
/// resolve the requested (or default) provider without knowing its type.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Provider identifier used for selection (e.g. "azure", "google", "ollama").
    /// Comparison is case-insensitive.
    /// </summary>
    string Name { get; }

    Task<EmbeddingResult> GenerateAsync(string text, CancellationToken cancellationToken = default);
}
