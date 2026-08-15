namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Root AI configuration, bound from the "AI" configuration section (docs task:
/// "Intent/Query Rewriting"). Replaces the single-provider-single-model shape
/// previously embedded directly under "AI:Embeddings:Providers:Azure" with a
/// reusable Connections + Models registry so multiple logical models (e.g.
/// "CategoryEmbedding", "IntentRewriter") can share the same underlying Azure
/// OpenAI resource. Dictionary keys (connection/model names) are compared
/// case-insensitively.
/// </summary>
public sealed class AIOptions
{
    public Dictionary<string, AIConnectionOptions> Connections { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, AIModelOptions> Models { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
