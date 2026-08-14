namespace Yunu.Commerce.AI.Application.Embeddings;

/// <summary>
/// Result of a text embedding generation call (docs/ai/ai-architecture.md §6,
/// "Provider Abstraction"). Contains only provider-agnostic facts about what
/// happened. <see cref="Dimensions"/> is derived from the vector length instead
/// of being stored separately, so it can never drift from the actual result.
/// </summary>
public sealed record EmbeddingResult(string Provider, string Model, float[] Embedding)
{
    public int Dimensions => Embedding.Length;
}
