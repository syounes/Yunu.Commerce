using Microsoft.Extensions.Options;

namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Validates <see cref="RerankingOptions"/> at startup (ValidateOnStart), so
/// a misconfigured reranker model or threshold fails fast instead of at first
/// request (docs task: "Contextual candidate reranking" §8), mirroring <see
/// cref="Yunu.Commerce.Catalog.Application.CategoryResolution.CategoryResolutionOptionsValidator"/>.
/// Does not verify the model is actually registered/Chat here (that is
/// enforced by <see cref="Yunu.Commerce.AI.Application.Configuration.IAIModelCatalog"/>
/// at first resolution, which already fails fast at DI-composition time for
/// singleton adapters); it only enforces the shape of this options section.
/// </summary>
public sealed class RerankingOptionsValidator : IValidateOptions<RerankingOptions>
{
    public ValidateOptionsResult Validate(string? name, RerankingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            return ValidateOptionsResult.Fail(
                "AI:Reranking:Model is required and must reference a logical model registered under \"AI:Models\" with ModelType = Chat.");
        }

        if (options.MinimumConfidence is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:Reranking:MinimumConfidence must be between 0 and 1.");
        }

        if (options.MinimumScoreMargin is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("AI:Reranking:MinimumScoreMargin must be between 0 and 1.");
        }

        if (options.MaximumCandidates < 2)
        {
            return ValidateOptionsResult.Fail("AI:Reranking:MaximumCandidates must be greater than or equal to 2.");
        }

        if (options.MaxConcurrentRerankRequests < 1)
        {
            return ValidateOptionsResult.Fail("AI:Reranking:MaxConcurrentRerankRequests must be greater than or equal to 1.");
        }

        if (!Enum.IsDefined(options.TechnicalFailureFallback))
        {
            return ValidateOptionsResult.Fail(
                $"AI:Reranking:TechnicalFailureFallback must be one of: {string.Join(", ", Enum.GetNames<TechnicalFailureFallbackStrategy>())}.");
        }

        return ValidateOptionsResult.Success;
    }
}
