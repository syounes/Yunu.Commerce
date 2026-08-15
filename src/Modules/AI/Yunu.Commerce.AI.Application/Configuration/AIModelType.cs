namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// Capability a logical AI model registration provides (docs task: "Intent/Query
/// Rewriting"). Used to fail fast when a model registered for one capability
/// (e.g. Chat) is requested for another (e.g. Embedding).
/// </summary>
public enum AIModelType
{
    Embedding,
    Chat
}
