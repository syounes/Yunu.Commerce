using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Reranking;

namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Validates <see cref="CategoryResolutionOptions"/> at startup
/// (ValidateOnStart), so a misconfigured threshold or missing embedding model
/// fails fast instead of at first request (docs task: "Google Category
/// Resolution"), mirroring <see
/// cref="Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeResolutionOptionsValidator"/>.
/// Also enforces that <see cref="CategoryResolutionOptions.TopK"/> is greater
/// than or equal to <see cref="RerankingOptions.MaximumCandidates"/>, so the
/// reranker is never starved of candidates that pgvector could have
/// retrieved.
/// </summary>
public sealed class CategoryResolutionOptionsValidator : IValidateOptions<CategoryResolutionOptions>
{
    private const int MaxTopK = 50;

    private readonly IOptions<RerankingOptions> _rerankingOptions;

    public CategoryResolutionOptionsValidator(IOptions<RerankingOptions> rerankingOptions)
    {
        _rerankingOptions = rerankingOptions;
    }

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

        var maximumCandidates = _rerankingOptions.Value.MaximumCandidates;

        if (options.TopK < maximumCandidates)
        {
            return ValidateOptionsResult.Fail(
                $"AI:CategoryResolution:TopK ({options.TopK}) must be greater than or equal to AI:Reranking:MaximumCandidates ({maximumCandidates}).");
        }

        return ValidateOptionsResult.Success;
    }
}
