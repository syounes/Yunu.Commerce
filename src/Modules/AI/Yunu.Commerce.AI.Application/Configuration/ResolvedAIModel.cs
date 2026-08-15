namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// A logical AI model registration merged with its connection's endpoint and
/// credential (docs task: "Intent/Query Rewriting"). This is what Infrastructure
/// adapters (e.g. the Azure OpenAI embedding provider or intent rewriter)
/// actually consume; they never read <see cref="AIOptions"/> directly.
/// </summary>
public sealed record ResolvedAIModel(
    string ModelName,
    string ConnectionName,
    string Endpoint,
    string ApiKey,
    string DeploymentName,
    AIModelType ModelType,
    int? Dimensions);
