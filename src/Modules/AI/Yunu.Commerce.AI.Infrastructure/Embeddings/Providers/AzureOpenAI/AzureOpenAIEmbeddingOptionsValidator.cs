using Microsoft.Extensions.Options;

namespace Yunu.Commerce.AI.Infrastructure.Embeddings.Providers.AzureOpenAI;

/// <summary>
/// Validates <see cref="AzureOpenAIEmbeddingOptions"/> at startup (ValidateOnStart)
/// so a misconfigured deployment fails fast instead of at first request. Never
/// includes the ApiKey value in any failure message.
/// </summary>
public sealed class AzureOpenAIEmbeddingOptionsValidator : IValidateOptions<AzureOpenAIEmbeddingOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureOpenAIEmbeddingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail("AI:Embeddings:Providers:Azure:Endpoint is required.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("AI:Embeddings:Providers:Azure:Endpoint must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("AI:Embeddings:Providers:Azure:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            return ValidateOptionsResult.Fail("AI:Embeddings:Providers:Azure:DeploymentName is required.");
        }

        if (options.Dimensions <= 0)
        {
            return ValidateOptionsResult.Fail("AI:Embeddings:Providers:Azure:Dimensions must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
