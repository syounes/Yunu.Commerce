namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// A logical AI model registration, bound from "AI:Models:{ModelName}"
/// (docs task: "Intent/Query Rewriting"). Points at the <see cref="Connection"/>
/// (by name) that supplies the endpoint/credential, plus the actual
/// deployment/model identifier to send to the provider. Logical names (e.g.
/// "CategoryEmbedding", "IntentRewriter") decouple business code from the
/// underlying vendor deployment name.
/// </summary>
public sealed record AIModelOptions
{
    public required string Connection { get; init; }

    public required string DeploymentName { get; init; }

    public required AIModelType ModelType { get; init; }

    /// <summary>
    /// Required and must be greater than zero when <see cref="ModelType"/> is
    /// <see cref="AIModelType.Embedding"/>; otherwise unused.
    /// </summary>
    public int? Dimensions { get; init; }
}
