using Xunit;
using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.AI.Application.Tests.IntentRewriting;

/// <summary>
/// Verifies the <see cref="IIntentRewriter"/> contract shape and default
/// locale behavior using a fake implementation (no Azure OpenAI call). The
/// Azure-specific mapping/deserialization logic lives in
/// AI.Infrastructure and is exercised there with fakes at the SDK boundary.
/// </summary>
public sealed class IntentRewriteContractTests
{
    private sealed class FakeIntentRewriter : IIntentRewriter
    {
        public IntentRewriteRequest? LastRequest { get; private set; }

        public Task<IntentRewriteResult> RewriteAsync(IntentRewriteRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(new IntentRewriteResult(
                OriginalInput: request.Input,
                NormalizedQuery: "Cadastrar um tênis masculino preto da Nike, tamanho 41, indicado para corrida.",
                SemanticQuery: "Tênis masculino preto para corrida, marca Nike, tamanho 41.",
                Intent: CatalogIntent.ProductCreation,
                DetectedLanguage: "pt",
                TargetLocale: request.Locale,
                CategoryHint: "Tênis para corrida",
                AttributeHints: new[]
                {
                    new AttributeHint("gênero", "masculino"),
                    new AttributeHint("cor", "preto"),
                    new AttributeHint("marca", "Nike"),
                    new AttributeHint("tamanho", "41"),
                    new AttributeHint("ocasião ou finalidade", "corrida")
                },
                SearchTerms: new[] { "tênis", "masculino", "preto", "Nike", "41", "corrida" },
                Confidence: 0.96m));
        }
    }

    [Fact]
    public async Task RewriteAsync_defaults_locale_to_pt_BR_when_not_specified()
    {
        var rewriter = new FakeIntentRewriter();

        var result = await rewriter.RewriteAsync(new IntentRewriteRequest("quero cadastrar um tenis"));

        Assert.Equal("pt-BR", result.TargetLocale);
    }

    [Fact]
    public async Task RewriteAsync_ProductCreation_extracts_attribute_hints_without_official_ids()
    {
        var rewriter = new FakeIntentRewriter();

        var result = await rewriter.RewriteAsync(
            new IntentRewriteRequest("quero cadastrar um tenis masculino preto nike tamanho 41 para corrida"));

        Assert.Equal(CatalogIntent.ProductCreation, result.Intent);
        Assert.Contains(result.AttributeHints, h => h.RawName == "marca" && h.RawValue == "Nike");
        Assert.InRange(result.Confidence, 0m, 1m);
    }

    [Fact]
    public async Task RewriteAsync_unknown_intent_preserves_normalized_input_and_uses_empty_arrays()
    {
        IIntentRewriter rewriter = new UnknownIntentRewriter();

        var result = await rewriter.RewriteAsync(new IntentRewriteRequest("asdkjaslkdj"));

        Assert.Equal(CatalogIntent.Unknown, result.Intent);
        Assert.Empty(result.AttributeHints);
        Assert.Empty(result.SearchTerms);
        Assert.True(result.Confidence < 0.5m);
    }

    private sealed class UnknownIntentRewriter : IIntentRewriter
    {
        public Task<IntentRewriteResult> RewriteAsync(IntentRewriteRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentRewriteResult(
                OriginalInput: request.Input,
                NormalizedQuery: request.Input,
                SemanticQuery: request.Input,
                Intent: CatalogIntent.Unknown,
                DetectedLanguage: "pt",
                TargetLocale: request.Locale,
                CategoryHint: null,
                AttributeHints: Array.Empty<AttributeHint>(),
                SearchTerms: Array.Empty<string>(),
                Confidence: 0.1m));
        }
    }
}
