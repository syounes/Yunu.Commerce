using Microsoft.Extensions.Options;

namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Validates <see cref="CategoryResolutionOptions"/> at startup
/// (ValidateOnStart), so a misconfigured threshold or missing embedding model
/// fails fast instead of at first request (docs task: "Google Category
/// Resolution"), mirroring <see
/// cref="Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeResolutionOptionsValidator"/>.
/// </summary>
public sealed class CategoryResolutionOptionsValidator : IValidateOptions<CategoryResolutionOptions>
{
    private const int MaxTopK = 50;

    public ValidateOptionsResult Validate(string? name, CategoryResolutionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.EmbeddingModel))
        {
            return ValidateOptionsResult.Fail(
                "AI:CategoryResolution:EmbeddingModel is required and must reference a logical model registered under \"AI:Models\" with ModelType = Embedding.");
        }

        if (options.TopK < 1)
        {
            return ValidateOptionsResult.Fail("AI:CategoryResolution:TopK must be greater than or equal to 1.");
        }

        if (options.TopK > MaxTopK)
        {
            return ValidateOptionsResult.Fail($"AI:CategoryResolution:TopK must be less than or equal to {MaxTopK}.");
        }

        if (options.MinimumSimilarity is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:CategoryResolution:MinimumSimilarity must be between 0 and 1.");
        }

        if (options.MinimumScoreMargin is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:CategoryResolution:MinimumScoreMargin must be between 0 and 1.");
        }

        return ValidateOptionsResult.Success;
    }
}
