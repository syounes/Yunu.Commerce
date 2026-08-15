namespace Yunu.Commerce.AI.Application.Configuration;

/// <summary>
/// A reusable AI provider connection (e.g. a single Azure OpenAI resource),
/// bound from "AI:Connections:{ConnectionName}". Multiple logical model
/// registrations (docs: "CategoryEmbedding", "IntentRewriter") can share the
/// same connection so the endpoint/credential is configured once per Azure
/// resource instead of once per deployment. <see cref="ApiKey"/> must always
/// come from an external secret store (.NET User Secrets locally, environment
/// variables such as AI__Connections__AzureOpenAI__ApiKey in other
/// environments); it must never be committed to appsettings.json,
/// appsettings.Development.json, docker-compose.yml or source code.
/// </summary>
public sealed record AIConnectionOptions
{
    public required string Endpoint { get; init; }

    public required string ApiKey { get; init; }
}
