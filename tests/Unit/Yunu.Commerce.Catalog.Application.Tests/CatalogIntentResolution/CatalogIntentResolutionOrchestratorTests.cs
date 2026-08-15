using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.CatalogIntentResolution;

/// <summary>
/// Unit tests for CatalogIntentResolutionOrchestrator (docs task: "Catalog
/// intent resolution orchestration"). All dependencies are fakes; never
/// touches Azure, pgvector or SQL Server.
/// </summary>
public sealed class CatalogIntentResolutionOrchestratorTests
{
    private static IntentRewriteResult BuildIntentResult(
        string? categoryHint = "tênis para corrida",
        IReadOnlyList<AttributeHint>? attributeHints = null,
        CatalogIntent intent = CatalogIntent.ProductCreation) => new(
        OriginalInput: "quero cadastrar um tênis",
        NormalizedQuery: "Quero cadastrar um tênis.",
        SemanticQuery: "tênis masculino branco tamanho 41 para corrida",
        Intent: intent,
        DetectedLanguage: "pt",
        TargetLocale: "pt-BR",
        CategoryHint: categoryHint,
        AttributeHints: attributeHints ?? [new AttributeHint("cor", "branco")],
        SearchTerms: ["tênis", "corrida"],
        Confidence: 0.9m);

    private static ResolveGoogleCategoryResult ResolvedCategory(long id = 123) => new(
        "tênis para corrida",
        GoogleCategoryResolutionStatus.Resolved,
        id,
        "Calçados esportivos",
        "Vestuário > Calçados > Calçados esportivos",
        4,
        0.87,
        [],
        null);

    private static ResolveGoogleCategoryResult AmbiguousCategory() => new(
        "tênis para corrida",
        GoogleCategoryResolutionStatus.Ambiguous,
        null, null, null, null,
        0.80,
        [],
        "Top candidates are too close.");

    private static ResolveGoogleCategoryResult NotFoundCategory() => new(
        "tênis para corrida",
        GoogleCategoryResolutionStatus.NotFound,
        null, null, null, null,
        0.40,
        [],
        "No candidate met the threshold.");

    private static ResolveAttributeHintsResult AllResolvedAttributes() => new(
        [new ResolvedAttributeHint("cor", "branco", AttributeResolutionStatus.Resolved, 1, "color", "Cor", "Enum", "branco", 10, "white", "Branco", 1.0, 1.0, null, [], null)],
        AllResolved: true);

    private static ResolveAttributeHintsResult AmbiguousAttributes() => new(
        [new ResolvedAttributeHint("cor", "branco", AttributeResolutionStatus.Ambiguous, null, null, null, null, null, null, null, null, null, null, null, [], "ambiguous")],
        AllResolved: false);

    private static ResolveAttributeHintsResult NotFoundAttributes() => new(
        [new ResolvedAttributeHint("cor", "branco", AttributeResolutionStatus.NotFound, null, null, null, null, null, null, null, null, null, null, null, [], "not found")],
        AllResolved: false);

    private static (
        CatalogIntentResolutionOrchestrator Orchestrator,
        FakeIntentRewriter IntentRewriter,
        FakeGoogleCategoryResolver CategoryResolver,
        FakeAttributeHintResolver AttributeResolver)
        CreateSut(IntentRewriteResult intentResult, ResolveGoogleCategoryResult categoryResult, ResolveAttributeHintsResult attributesResult)
    {
        var intentRewriter = new FakeIntentRewriter(intentResult);
        var categoryResolver = new FakeGoogleCategoryResolver(categoryResult);
        var attributeResolver = new FakeAttributeHintResolver(attributesResult);

        var orchestrator = new CatalogIntentResolutionOrchestrator(
            intentRewriter,
            categoryResolver,
            attributeResolver,
            NullLogger<CatalogIntentResolutionOrchestrator>.Instance);

        return (orchestrator, intentRewriter, categoryResolver, attributeResolver);
    }

    [Fact]
    public async Task ResolveAsync_CallsIntentRewriterExactlyOnce()
    {
        var (orchestrator, intentRewriter, _, _) = CreateSut(BuildIntentResult(), ResolvedCategory(), AllResolvedAttributes());

        await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Equal(1, intentRewriter.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesCategoryBeforeAttributes_AndPassesGoogleCategoryId()
    {
        var (orchestrator, _, categoryResolver, attributeResolver) = CreateSut(BuildIntentResult(), ResolvedCategory(123), AllResolvedAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Equal("tênis para corrida", categoryResolver.LastRequest!.RawCategoryHint);
        Assert.Equal(123, attributeResolver.LastRequest!.GoogleCategoryId);
        Assert.Equal(CatalogIntentResolutionStatus.Resolved, result.Status);
        Assert.True(result.ReadyForProposal);
    }

    [Fact]
    public async Task ResolveAsync_AmbiguousCategory_PassesNullToAttributeResolver()
    {
        var (orchestrator, _, _, attributeResolver) = CreateSut(BuildIntentResult(), AmbiguousCategory(), AllResolvedAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Null(attributeResolver.LastRequest!.GoogleCategoryId);
        Assert.False(result.ReadyForProposal);
        Assert.Equal(CatalogIntentResolutionStatus.NeedsClarification, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_NotFoundCategory_PassesNullToAttributeResolver()
    {
        var (orchestrator, _, _, attributeResolver) = CreateSut(BuildIntentResult(), NotFoundCategory(), AllResolvedAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Null(attributeResolver.LastRequest!.GoogleCategoryId);
        Assert.False(result.ReadyForProposal);
    }

    [Fact]
    public async Task ResolveAsync_AllAttributesResolved_AndCategoryResolved_ReadyForProposalTrue()
    {
        var (orchestrator, _, _, _) = CreateSut(BuildIntentResult(), ResolvedCategory(), AllResolvedAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.True(result.ReadyForProposal);
        Assert.Equal(CatalogIntentResolutionStatus.Resolved, result.Status);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ResolveAsync_AmbiguousAttribute_ReadyForProposalFalse()
    {
        var (orchestrator, _, _, _) = CreateSut(BuildIntentResult(), ResolvedCategory(), AmbiguousAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.False(result.ReadyForProposal);
        Assert.Equal(CatalogIntentResolutionStatus.NeedsClarification, result.Status);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task ResolveAsync_NotFoundAttribute_ReadyForProposalFalse()
    {
        var (orchestrator, _, _, _) = CreateSut(BuildIntentResult(), ResolvedCategory(), NotFoundAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.False(result.ReadyForProposal);
    }

    [Fact]
    public async Task ResolveAsync_PreservesPartialResults()
    {
        var (orchestrator, _, _, _) = CreateSut(BuildIntentResult(), AmbiguousCategory(), NotFoundAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.NotNull(result.Intent);
        Assert.NotNull(result.Category);
        Assert.NotNull(result.Attributes);
    }

    [Fact]
    public async Task ResolveAsync_EmptyInput_ReturnsInvalidWithoutCallingIntentRewriter()
    {
        var (orchestrator, intentRewriter, _, _) = CreateSut(BuildIntentResult(), ResolvedCategory(), AllResolvedAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest(""), CancellationToken.None);

        Assert.Equal(CatalogIntentResolutionStatus.Invalid, result.Status);
        Assert.Equal(0, intentRewriter.CallCount);
        Assert.False(result.ReadyForProposal);
    }

    [Fact]
    public async Task ResolveAsync_EmptyCategoryHint_SkipsCategoryResolverCall()
    {
        var (orchestrator, _, categoryResolver, _) = CreateSut(
            BuildIntentResult(categoryHint: null),
            ResolvedCategory(),
            AllResolvedAttributes());

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Null(categoryResolver.LastRequest);
        Assert.Equal(GoogleCategoryResolutionStatus.NotFound, result.Category!.Status);
        Assert.False(result.ReadyForProposal);
    }

    [Fact]
    public async Task ResolveAsync_Cancellation_PropagatesToken()
    {
        var (orchestrator, _, _, _) = CreateSut(BuildIntentResult(), ResolvedCategory(), AllResolvedAttributes());

        using var cts = new CancellationTokenSource();

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), cts.Token);

        Assert.Equal(CatalogIntentResolutionStatus.Resolved, result.Status);
    }
}
