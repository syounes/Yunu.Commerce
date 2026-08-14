namespace Yunu.Commerce.Api.AI;

/// <summary>
/// HTTP response contract for the Google category embedding smoke test
/// (docs task: "AI Embeddings smoke test"). Provider-agnostic: no vendor-specific
/// naming (e.g. "Deployment") is exposed. Dimensions is always computed from
/// the actual returned vector length, never hardcoded.
/// </summary>
public sealed class GenerateGoogleCategoryEmbeddingResponse
{
    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required int Dimensions { get; init; }

    public required float[] Embedding { get; init; }
}
