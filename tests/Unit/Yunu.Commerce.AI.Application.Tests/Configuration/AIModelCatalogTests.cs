using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.AI.Application.Configuration;

namespace Yunu.Commerce.AI.Application.Tests.Configuration;

public sealed class AIModelCatalogTests
{
    private static AIModelCatalog CreateCatalog(AIOptions options)
    {
        return new AIModelCatalog(Options.Create(options));
    }

    [Fact]
    public void Resolve_returns_endpoint_apikey_and_deployment_from_shared_connection()
    {
        var options = new AIOptions
        {
            Connections =
            {
                ["AzureOpenAI"] = new AIConnectionOptions { Endpoint = "https://example.openai.azure.com/openai/v1/", ApiKey = "secret" }
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

        var catalog = CreateCatalog(options);

        var resolved = catalog.Resolve("IntentRewriter", AIModelType.Chat);

        Assert.Equal("https://example.openai.azure.com/openai/v1/", resolved.Endpoint);
        Assert.Equal("secret", resolved.ApiKey);
        Assert.Equal("yunu-intent-rewriter-v1", resolved.DeploymentName);
        Assert.Equal("AzureOpenAI", resolved.ConnectionName);
    }

    [Fact]
    public void Resolve_throws_when_model_is_not_registered()
    {
        var catalog = CreateCatalog(new AIOptions());

        var ex = Assert.Throws<AIModelResolutionException>(() => catalog.Resolve("Missing", AIModelType.Chat));

        Assert.Contains("Missing", ex.Message);
    }

    [Fact]
    public void Resolve_throws_when_model_points_at_unregistered_connection()
    {
        var options = new AIOptions
        {
            Models =
            {
                ["IntentRewriter"] = new AIModelOptions
                {
                    Connection = "DoesNotExist",
                    DeploymentName = "yunu-intent-rewriter-v1",
                    ModelType = AIModelType.Chat
                }
            }
        };

        var catalog = CreateCatalog(options);

        Assert.Throws<AIModelResolutionException>(() => catalog.Resolve("IntentRewriter", AIModelType.Chat));
    }

    [Fact]
    public void Resolve_throws_when_requested_capability_does_not_match_registration()
    {
        var options = new AIOptions
        {
            Connections =
            {
                ["AzureOpenAI"] = new AIConnectionOptions { Endpoint = "https://example.openai.azure.com/openai/v1/", ApiKey = "secret" }
            },
            Models =
            {
                ["CategoryEmbedding"] = new AIModelOptions
                {
                    Connection = "AzureOpenAI",
                    DeploymentName = "yunu-embedding-category-v1",
                    ModelType = AIModelType.Embedding,
                    Dimensions = 1536
                }
            }
        };

        var catalog = CreateCatalog(options);

        Assert.Throws<AIModelResolutionException>(() => catalog.Resolve("CategoryEmbedding", AIModelType.Chat));
    }
}
