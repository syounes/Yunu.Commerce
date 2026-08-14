namespace Yunu.Commerce.Api.AI;

/// <summary>
/// HTTP request contract for the Google category embedding smoke test
/// (docs task: "AI Embeddings smoke test"). <see cref="Provider"/> is optional:
/// when omitted, the orchestrator falls back to the configured default provider.
/// </summary>
public sealed class GenerateGoogleCategoryEmbeddingRequest
{
    public required string Text { get; init; }

    public string? Provider { get; init; }
}
