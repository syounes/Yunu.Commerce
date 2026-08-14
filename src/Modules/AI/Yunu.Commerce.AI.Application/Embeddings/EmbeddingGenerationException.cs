namespace Yunu.Commerce.AI.Application.Embeddings;

/// <summary>
/// Raised when a text embedding provider adapter fails to produce a usable
/// embedding, including when the returned vector does not match the expected
/// dimensionality. The API layer translates this into a provider/internal error
/// response (docs task: "AI Embeddings smoke test").
/// </summary>
public sealed class EmbeddingGenerationException : Exception
{
    public EmbeddingGenerationException(string message) : base(message)
    {
    }
}
