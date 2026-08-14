namespace Yunu.Commerce.AI.Infrastructure.Embeddings.Providers.AzureOpenAI;

/// <summary>
/// Environment-specific Azure OpenAI configuration for text embedding generation
/// (docs/adr/0008-genai-provider-abstraction.md). Bound from the
/// "AI:Embeddings:Providers:Azure" configuration section. ApiKey must always
/// come from an external secret store (.NET User Secrets locally); it must
/// never be committed to appsettings.json, appsettings.Development.json,
/// docker-compose.yml or source code. <see cref="DeploymentName"/> doubles as
/// the provider's model identifier since Azure OpenAI addresses models by
/// deployment name.
/// </summary>
public sealed class AzureOpenAIEmbeddingOptions
{
    public required string Endpoint { get; init; }

    public required string ApiKey { get; init; }

    public required string DeploymentName { get; init; }

    public required int Dimensions { get; init; }
}
