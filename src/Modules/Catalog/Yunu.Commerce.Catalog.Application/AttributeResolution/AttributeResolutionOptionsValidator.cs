using Microsoft.Extensions.Options;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Validates <see cref="AttributeResolutionOptions"/> at startup
/// (ValidateOnStart), so a misconfigured threshold or missing embedding model
/// fails fast instead of at first request (docs task: "Semantic attribute
/// hint resolution").
/// </summary>
public sealed class AttributeResolutionOptionsValidator : IValidateOptions<AttributeResolutionOptions>
{
    public ValidateOptionsResult Validate(string? name, AttributeResolutionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            return ValidateOptionsResult.Fail(
                "AI:AttributeResolution:EmbeddingModel is required and must reference a logical model registered under \"AI:Models\" with ModelType = Embedding.");
        }

        if (options.TopK < 1)
        {
            return ValidateOptionsResult.Fail("AI:AttributeResolution:TopK must be greater than or equal to 1.");
        }

        if (options.DefinitionMinimumSimilarity is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:AttributeResolution:DefinitionMinimumSimilarity must be between 0 and 1.");
        }

        if (options.OptionMinimumSimilarity is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:AttributeResolution:OptionMinimumSimilarity must be between 0 and 1.");
        }

        if (options.MinimumScoreMargin is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:AttributeResolution:MinimumScoreMargin must be between 0 and 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
