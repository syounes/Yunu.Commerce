using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

namespace Yunu.Commerce.IntegrationTests;

/// <summary>
/// Opt-in integration test exercising the real "yunu-intent-rewriter-v1"
/// Azure OpenAI deployment (docs task: "Intent/Query Rewriting"). Skipped
/// automatically when no API key is configured (locally via .NET User
/// Secrets, in CI via the AI__Connections__AzureOpenAI__ApiKey environment
/// variable), so the unit test suite never depends on Azure connectivity.
/// </summary>
public sealed class AzureOpenAIIntentRewriterIntegrationTests
{
    private static AIOptions? TryLoadOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AzureOpenAIIntentRewriterIntegrationTests>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var apiKey = configuration["AI:Connections:AzureOpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return new AIOptions
        {
            Connections =
            {
                ["AzureOpenAI"] = new AIConnectionOptions
                {
                    Endpoint = configuration["AI:Connections:AzureOpenAI:Endpoint"]
                        ?? "https://aif-yunu-commerce-lab.openai.azure.com/openai/v1/",
                    ApiKey = apiKey
                }
            },
            Models =
            {
                ["IntentRewriter"] = new AIModelOptions
                {
                    Connection = "AzureOpenAI",
                    DeploymentName = "yunu-intent-rewriter-v1",
                    ModelType = AIModelType.Chat
                }
            }
        };
    }

    [Fact]
    public async Task RewriteAsync_against_real_deployment_classifies_product_creation()
    {
        var options = TryLoadOptions();

        if (options is null)
        {
            return; // Skipped: no AI:Connections:AzureOpenAI:ApiKey configured in this environment.
        }

        var catalog = new AIModelCatalog(Microsoft.Extensions.Options.Options.Create(options));
        var rewriter = new AzureOpenAIIntentRewriter(catalog, NullLogger<AzureOpenAIIntentRewriter>.Instance);

        var result = await rewriter.RewriteAsync(
            new IntentRewriteRequest("quero cadastrar um tenis masculino preto nike tamanho 41 para corrida"));

        Assert.Equal(CatalogIntent.ProductCreation, result.Intent);
        Assert.InRange(result.Confidence, 0m, 1m);
    }
}
