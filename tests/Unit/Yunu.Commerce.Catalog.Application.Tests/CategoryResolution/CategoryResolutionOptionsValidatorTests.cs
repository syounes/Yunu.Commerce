using Xunit;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

public sealed class CategoryResolutionOptionsValidatorTests
{
    private readonly CategoryResolutionOptionsValidator _validator = new();

    private static CategoryResolutionOptions ValidOptions() => new()
    {
        EmbeddingModel = "CategoryEmbedding",
        TopK = 5,
        MinimumSimilarity = 0.70,
        MinimumScoreMargin = 0.03,
        IncludeCandidatesInResponse = true
    };

    [Fact]
    public void Validate_succeeds_for_well_formed_options()
    {
        var result = _validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_embedding_model_is_missing()
    {
        var options = new CategoryResolutionOptions
        {
            EmbeddingModel = "",
            TopK = 5,
            MinimumSimilarity = 0.70,
            MinimumScoreMargin = 0.03,
            IncludeCandidatesInResponse = true
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_topK_is_zero()
    {
        var options = new CategoryResolutionOptions
        {
            EmbeddingModel = "CategoryEmbedding",
            TopK = 0,
            MinimumSimilarity = 0.70,
            MinimumScoreMargin = 0.03,
            IncludeCandidatesInResponse = true
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_topK_exceeds_safe_limit()
    {
        var options = new CategoryResolutionOptions
        {
            EmbeddingModel = "CategoryEmbedding",
            TopK = 1000,
            MinimumSimilarity = 0.70,
            MinimumScoreMargin = 0.03,
            IncludeCandidatesInResponse = true
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_fails_for_out_of_range_minimum_similarity(double value)
    {
        var options = new CategoryResolutionOptions
        {
            EmbeddingModel = "CategoryEmbedding",
            TopK = 5,
            MinimumSimilarity = value,
            MinimumScoreMargin = 0.03,
            IncludeCandidatesInResponse = true
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_fails_for_out_of_range_margin(double value)
    {
        var options = new CategoryResolutionOptions
        {
            EmbeddingModel = "CategoryEmbedding",
            TopK = 5,
            MinimumSimilarity = 0.70,
            MinimumScoreMargin = value,
            IncludeCandidatesInResponse = true
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
    }
}
