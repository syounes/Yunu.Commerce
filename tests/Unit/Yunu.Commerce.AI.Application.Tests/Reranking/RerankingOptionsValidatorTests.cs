using Xunit;
using Yunu.Commerce.AI.Application.Reranking;

namespace Yunu.Commerce.AI.Application.Tests.Reranking;

/// <summary>
/// Unit tests for <see cref="RerankingOptionsValidator"/> (docs task:
/// "Contextual candidate reranking" §8, fail-fast validation).
/// </summary>
public sealed class RerankingOptionsValidatorTests
{
    private readonly RerankingOptionsValidator _validator = new();

    private static RerankingOptions ValidOptions() => new()
    {
        Model = "CatalogReranker",
        MinimumConfidence = 0.75,
        MinimumScoreMargin = 0.10,
        MaximumCandidates = 10,
        AlwaysRerankSemanticMatches = true,
        MaxConcurrentRerankRequests = 4,
        TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
    };

    [Fact]
    public void Validate_succeeds_for_well_formed_options()
    {
        var result = _validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_model_is_missing()
    {
        var options = new RerankingOptions
        {
            Model = "",
            MinimumConfidence = 0.75,
            MinimumScoreMargin = 0.10,
            MaximumCandidates = 10,
            AlwaysRerankSemanticMatches = true,
            MaxConcurrentRerankRequests = 4,
            TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
        };

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_fails_when_minimum_confidence_out_of_range(double value)
    {
        var options = new RerankingOptions
        {
            Model = "CatalogReranker",
            MinimumConfidence = value,
            MinimumScoreMargin = 0.10,
            MaximumCandidates = 10,
            AlwaysRerankSemanticMatches = true,
            MaxConcurrentRerankRequests = 4,
            TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
        };

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_fails_when_minimum_score_margin_out_of_range(double value)
    {
        var options = new RerankingOptions
        {
            Model = "CatalogReranker",
            MinimumConfidence = 0.75,
            MinimumScoreMargin = value,
            MaximumCandidates = 10,
            AlwaysRerankSemanticMatches = true,
            MaxConcurrentRerankRequests = 4,
            TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
        };

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_fails_when_maximum_candidates_below_two()
    {
        var options = new RerankingOptions
        {
            Model = "CatalogReranker",
            MinimumConfidence = 0.75,
            MinimumScoreMargin = 0.10,
            MaximumCandidates = 1,
            AlwaysRerankSemanticMatches = true,
            MaxConcurrentRerankRequests = 4,
            TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
        };

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_fails_when_max_concurrent_rerank_requests_below_one()
    {
        var options = new RerankingOptions
        {
            Model = "CatalogReranker",
            MinimumConfidence = 0.75,
            MinimumScoreMargin = 0.10,
            MaximumCandidates = 10,
            AlwaysRerankSemanticMatches = true,
            MaxConcurrentRerankRequests = 0,
            TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
        };

        Assert.True(_validator.Validate(null, options).Failed);
    }
}
