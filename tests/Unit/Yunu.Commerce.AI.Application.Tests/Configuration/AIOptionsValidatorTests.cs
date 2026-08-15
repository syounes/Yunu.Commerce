using Xunit;
using Yunu.Commerce.AI.Application.Configuration;

namespace Yunu.Commerce.AI.Application.Tests.Configuration;

public sealed class AIOptionsValidatorTests
{
    private readonly AIOptionsValidator _validator = new();

    private static AIOptions ValidOptions()
    {
        return new AIOptions
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
                },
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
    public void Validate_succeeds_for_well_formed_options()
    {
        var result = _validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_model_references_missing_connection()
    {
        var options = ValidOptions();
        options.Models["IntentRewriter"] = options.Models["IntentRewriter"] with { Connection = "DoesNotExist" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_fails_when_deployment_name_is_missing()
    {
        var options = ValidOptions();
        options.Models["IntentRewriter"] = options.Models["IntentRewriter"] with { DeploymentName = "" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_fails_when_embedding_model_has_no_dimensions()
    {
        var options = ValidOptions();
        options.Models["CategoryEmbedding"] = options.Models["CategoryEmbedding"] with { Dimensions = null };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_fails_when_connection_endpoint_is_not_absolute_uri()
    {
        var options = ValidOptions();
        options.Connections["AzureOpenAI"] = options.Connections["AzureOpenAI"] with { Endpoint = "not-a-uri" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_fails_when_connection_api_key_is_missing()
    {
        var options = ValidOptions();
        options.Connections["AzureOpenAI"] = options.Connections["AzureOpenAI"] with { ApiKey = "" };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}
