using Xunit;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.ProductProposals;
using Yunu.Commerce.Catalog.Domain.ProductProposals;

namespace Yunu.Commerce.Catalog.Application.Tests.ProductProposals;

/// <summary>
/// Unit tests for CreateProductProposalHandler (docs task: "Catalog intent
/// resolution orchestration" - proposal persistence). All dependencies are
/// fakes; never touches Azure OpenAI, MongoDB, pgvector or SQL Server. The
/// orchestrator itself is faked to guarantee no real LLM call happens.
/// </summary>
public sealed class CreateProductProposalHandlerTests
{
    private static IntentRewriteResult BuildIntent(
        CatalogIntent intent = CatalogIntent.ProductCreation,
        string? categoryHint = "microfone condensador USB") => new(
        OriginalInput: "Quero cadastrar um microfone condensador USB preto",
        NormalizedQuery: "Quero cadastrar um microfone condensador USB preto.",
        SemanticQuery: "microfone condensador USB preto novo",
        Intent: intent,
        DetectedLanguage: "pt",
        TargetLocale: "pt-BR",
        CategoryHint: categoryHint,
        AttributeHints: [new AttributeHint("cor", "preto")],
        SearchTerms: ["microfone", "USB"],
        Confidence: 0.92m,
        CategorySearchQuery: "microfone condensador USB");

    private static ResolveGoogleCategoryResult ResolvedCategory(long id = 200) => new(
        "microfone condensador USB",
        GoogleCategoryResolutionStatus.Resolved,
        id,
        "Microfones",
        "Eletrônicos > Áudio > Microfones",
        3,
        0.91,
        [],
        null,
        ResolutionStrategy.VectorOnly,
        null,
        null);

    private static ResolveAttributeHintsResult AllResolvedAttributes(params ResolvedAttributeHint[] hints) =>
        new(hints, AllResolved: true);

    private static ResolvedAttributeHint EnumHint(
        string rawName,
        string rawValue,
        int definitionId,
        string code,
        string name,
        int optionId,
        string optionCode,
        string optionName) => new(
        rawName, rawValue, AttributeResolutionStatus.Resolved,
        definitionId, code, name, "Enum", optionCode,
        optionId, optionCode, optionName,
        1.0, 1.0, null, [], null);

    private static ResolvedAttributeHint MeasurementHint(
        int definitionId,
        string code,
        string name,
        decimal value,
        string unit) => new(
        "peso", $"{value} {unit}", AttributeResolutionStatus.Resolved,
        definitionId, code, name, "Measurement", $"{value} {unit}",
        null, null, null,
        0.95, 0.95, null, [], null)
    {
        TypedValue = new ResolvedAttributeValue(
            DisplayValue: $"{value} {unit}",
            MeasurementValue: value,
            UnitCode: unit)
    };

    private static CatalogIntentResolutionResult ReadyResult(
        IntentRewriteResult? intent = null,
        ResolveGoogleCategoryResult? category = null,
        ResolveAttributeHintsResult? attributes = null,
        CatalogIntentResolutionStatus status = CatalogIntentResolutionStatus.Resolved,
        bool readyForProposal = true) => new(
        status,
        intent ?? BuildIntent(),
        category ?? ResolvedCategory(),
        attributes ?? AllResolvedAttributes(),
        readyForProposal,
        Warnings: []);

    private static (CreateProductProposalHandler Handler, FakeCatalogIntentResolutionOrchestrator Orchestrator, FakeProductProposalRepository Repository)
        CreateSut(CatalogIntentResolutionResult result)
    {
        var orchestrator = new FakeCatalogIntentResolutionOrchestrator(result);
        var repository = new FakeProductProposalRepository();
        var handler = new CreateProductProposalHandler(orchestrator, repository);
        return (handler, orchestrator, repository);
    }

    [Fact]
    public async Task HandleAsync_WhenResolutionIsReady_CreatesProposalWithAwaitingReviewStatus()
    {
        var (handler, _, repository) = CreateSut(ReadyResult());

        var result = await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        Assert.Equal(ProductProposalStatus.AwaitingReview.ToString(), result.Status);
        Assert.Equal(ProductProposalStatus.AwaitingReview, repository.LastAdded!.Status);
    }

    [Fact]
    public async Task HandleAsync_CallsOrchestratorExactlyOnce()
    {
        var (handler, orchestrator, _) = CreateSut(ReadyResult());

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        Assert.Equal(1, orchestrator.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenResolutionIsReady_PersistsProposal()
    {
        var (handler, _, repository) = CreateSut(ReadyResult());

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        Assert.Equal(1, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenReadyForProposalIsFalse_DoesNotPersistAndThrows()
    {
        var (handler, _, repository) = CreateSut(ReadyResult(readyForProposal: false));

        await Assert.ThrowsAsync<ProductProposalResolutionException>(() =>
            handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryIsNotResolved_DoesNotPersistAndThrows()
    {
        var ambiguousCategory = new ResolveGoogleCategoryResult(
            "microfone", GoogleCategoryResolutionStatus.Ambiguous, null, null, null, null, 0.5, [], "ambiguous");

        var (handler, _, repository) = CreateSut(ReadyResult(category: ambiguousCategory));

        await Assert.ThrowsAsync<ProductProposalResolutionException>(() =>
            handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenAnyAttributeIsNotResolved_DoesNotPersistAndThrows()
    {
        var notFoundAttributes = new ResolveAttributeHintsResult(
            [new ResolvedAttributeHint("cor", "preto", AttributeResolutionStatus.NotFound, null, null, null, null, null, null, null, null, null, null, null, [], "not found")],
            AllResolved: false);

        var (handler, _, repository) = CreateSut(ReadyResult(attributes: notFoundAttributes));

        await Assert.ThrowsAsync<ProductProposalResolutionException>(() =>
            handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task HandleAsync_PersistsCorrectGoogleCategorySnapshot()
    {
        var category = ResolvedCategory(id: 555);
        var (handler, _, repository) = CreateSut(ReadyResult(category: category));

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var proposedCategory = repository.LastAdded!.Product.GoogleCategory;
        Assert.Equal(555, proposedCategory.GoogleCategoryId);
        Assert.Equal(category.CategoryName, proposedCategory.Name);
        Assert.Equal(category.CategoryPath, proposedCategory.Path);
        Assert.Equal(category.Depth, proposedCategory.Depth);
    }

    [Fact]
    public async Task HandleAsync_PreservesUsbAsOptionAttribute()
    {
        var attributes = AllResolvedAttributes(
            EnumHint("conectividade", "USB", 1, "connectivity", "Conectividade", 10, "USB", "USB"));

        var (handler, _, repository) = CreateSut(ReadyResult(attributes: attributes));

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var attribute = repository.LastAdded!.Skus.Single().Attributes.Single();
        Assert.Equal("USB", attribute.OptionCode);
        Assert.Equal(10, attribute.AttributeOptionId!.Value.Value);
    }

    [Fact]
    public async Task HandleAsync_PreservesNewAsConditionOption()
    {
        var attributes = AllResolvedAttributes(
            EnumHint("condicao", "novo", 2, "condition", "Condição", 20, "NEW", "Novo"));

        var (handler, _, repository) = CreateSut(ReadyResult(attributes: attributes));

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var attribute = repository.LastAdded!.Skus.Single().Attributes.Single();
        Assert.Equal("NEW", attribute.OptionCode);
        Assert.Equal("Novo", attribute.OptionName);
    }

    [Fact]
    public async Task HandleAsync_PreservesMeasurementAs850Grams()
    {
        var attributes = AllResolvedAttributes(
            MeasurementHint(3, "weight", "Peso", 850m, "g"));

        var (handler, _, repository) = CreateSut(ReadyResult(attributes: attributes));

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var attribute = repository.LastAdded!.Skus.Single().Attributes.Single();
        Assert.Equal(850m, attribute.TypedValue!.MeasurementValue);
        Assert.Equal("g", attribute.TypedValue!.UnitCode);
        Assert.Equal("850 g", attribute.TypedValue!.DisplayValue);
    }

    [Fact]
    public async Task HandleAsync_PreservesDimensionsInCentimeters()
    {
        var attributes = AllResolvedAttributes(
            MeasurementHint(4, "length", "Comprimento", 12.5m, "cm"));

        var (handler, _, repository) = CreateSut(ReadyResult(attributes: attributes));

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var attribute = repository.LastAdded!.Skus.Single().Attributes.Single();
        Assert.Equal(12.5m, attribute.TypedValue!.MeasurementValue);
        Assert.Equal("cm", attribute.TypedValue!.UnitCode);
    }

    [Fact]
    public async Task HandleAsync_AssignsDeterministicSequenceForRepeatedDefinition()
    {
        var first = EnumHint("dimensao1", "12", 5, "dimension", "Dimensão", 30, "12CM", "12 cm");
        var second = EnumHint("dimensao2", "20", 5, "dimension", "Dimensão", 31, "20CM", "20 cm");

        var attributes = AllResolvedAttributes(first, second);

        var (handler, _, repository) = CreateSut(ReadyResult(attributes: attributes));

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var mapped = repository.LastAdded!.Skus.Single().Attributes.ToArray();
        Assert.Equal(1, mapped[0].Sequence);
        Assert.Equal(2, mapped[1].Sequence);
    }

    [Fact]
    public async Task HandleAsync_DoesNotInventNameDescriptionCodeOrGtin()
    {
        var (handler, _, repository) = CreateSut(ReadyResult());

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), CancellationToken.None);

        var proposal = repository.LastAdded!;
        Assert.Null(proposal.Product.SuggestedName);
        Assert.Null(proposal.Product.Description);
        var sku = proposal.Skus.Single();
        Assert.Null(sku.SuggestedCode);
        Assert.Null(sku.Gtin);
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationToOrchestrator()
    {
        using var cts = new CancellationTokenSource();
        var orchestrator = new FakeCatalogIntentResolutionOrchestrator(ReadyResult(), cts.Token);
        var repository = new FakeProductProposalRepository();
        var handler = new CreateProductProposalHandler(orchestrator, repository);

        await handler.HandleAsync(new CreateProductProposalCommand("input", "pt-BR"), cts.Token);

        Assert.Equal(cts.Token, orchestrator.ReceivedCancellationToken);
    }
}
