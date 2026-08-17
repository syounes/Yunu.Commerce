using System.Reflection;
using OpenAI.Chat;
using Xunit;
using Yunu.Commerce.AI.Infrastructure.Reranking.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure.Tests.Reranking;

/// <summary>
/// Regression tests for the shared, generic candidate reranker system prompt
/// (docs task: "Google Category reranking hardening"). This prompt is used
/// as-is by Google Category, AttributeDefinition and AttributeOption
/// reranking; Google-Category-specific disambiguation rules must live only
/// in the per-request <c>Task</c> text (see
/// <see cref="Yunu.Commerce.Catalog.Application.CategoryResolution.GoogleCategoryRerankInstructions"/>),
/// never here.
/// </summary>
public sealed class CandidateRerankerSystemPromptTests
{
    [Fact]
    public void SharedPrompt_stays_generic_and_is_not_versioned_for_GoogleCategory_only_rules()
    {
        Assert.Equal("v1", CandidateRerankerSystemPrompt.Version);
    }

    [Fact]
    public void SharedPrompt_does_not_hardcode_any_specific_polysemous_term_or_official_id()
    {
        // The shared prompt may keep its own pre-existing, generic
        // running-shoes-vs-sporting-goods illustration, but must never
        // reference an official GoogleCategoryId or a term-specific
        // production rule such as "tênis" -> 187.
        Assert.DoesNotContain("187", CandidateRerankerSystemPrompt.Text);
        Assert.DoesNotContain("1065", CandidateRerankerSystemPrompt.Text);
    }

    [Fact]
    public void RerankerRequest_uses_lowest_supported_temperature_for_determinism()
    {
        var field = typeof(AzureOpenAICandidateReranker).GetField(
            "CompletionOptionsTemplate",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var options = (ChatCompletionOptions)field!.GetValue(null)!;

        Assert.Equal(0f, options.Temperature);
    }
}
