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
        CatalogIntent intent = CatalogIntent.ProductCreation,
        string? categorySearchQuery = "sapatos esportivos para corrida") => new(
        OriginalInput: "quero cadastrar um tênis",
        NormalizedQuery: "Quero cadastrar um tênis.",
        SemanticQuery: "tênis masculino branco tamanho 41 para corrida",
        Intent: intent,
        DetectedLanguage: "pt",
        TargetLocale: "pt-BR",
        CategoryHint: categoryHint,
        AttributeHints: attributeHints ?? [new AttributeHint("cor", "branco")],
        SearchTerms: ["tênis", "corrida"],
        Confidence: 0.9m,
        CategorySearchQuery: categorySearchQuery);

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
    public async Task ResolveAsync_ForwardsCategorySearchQuery_ToCategoryResolver()
    {
        var (orchestrator, _, categoryResolver, _) = CreateSut(
            BuildIntentResult(categorySearchQuery: "sapatos esportivos para corrida"),
            ResolvedCategory(),
            AllResolvedAttributes());

        await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Equal("sapatos esportivos para corrida", categoryResolver.LastRequest!.CategorySearchQuery);
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

    [Fact]
    public async Task ResolveAsync_RunningShoesFullExtractionRegression_AllSixHintsForwarded_CategoryStaysSapatos()
    {
        // Regression for the pt-BR "tênis para corrida" extraction bug: all
        // six explicit facts (gênero, cor, tamanho, uso, estado, peso para
        // entrega) must reach the Attribute Resolver even though the
        // categorySearchQuery removed several of them for category retrieval
        // purposes only. Category must remain 187 (Sapatos), never 1065.
        var intentResult = new IntentRewriteResult(
            OriginalInput: "Quero cadastrar um tênis masculino branco, tamanho 41, para corrida, produto novo e com peso para entrega de 2 kg.",
            NormalizedQuery: "Quero cadastrar um tênis masculino branco, tamanho 41, para corrida, produto novo e com peso para entrega de 2 kg.",
            SemanticQuery: "Cadastrar tênis masculino branco tamanho 41 para corrida, produto novo, peso para entrega 2 kg",
            Intent: CatalogIntent.ProductCreation,
            DetectedLanguage: "pt",
            TargetLocale: "pt-BR",
            CategoryHint: "tênis para corrida",
            AttributeHints:
            [
                new AttributeHint("gênero", "masculino"),
                new AttributeHint("cor", "branco"),
                new AttributeHint("tamanho", "41"),
                new AttributeHint("uso", "corrida"),
                new AttributeHint("estado", "novo"),
                new AttributeHint("peso para entrega", "2 kg")
            ],
            SearchTerms: ["tênis", "corrida", "masculino", "branco"],
            Confidence: 0.9m,
            CategorySearchQuery: "sapatos esportivos para corrida");

        var categoryResult = new ResolveGoogleCategoryResult(
            "tênis para corrida",
            GoogleCategoryResolutionStatus.Resolved,
            187,
            "Sapatos",
            "Vestuário e acessórios > Sapatos",
            2,
            0.42,
            [],
            null,
            ResolutionStrategy.Reranked,
            0.9,
            "O produto é um calçado esportivo para corrida.");

        var attributesResult = new ResolveAttributeHintsResult(
            [
                new ResolvedAttributeHint("gênero", "masculino", AttributeResolutionStatus.Resolved, 1, "gender", "Gênero", "Enum", "MALE", 10, "MALE", "Masculino", 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("cor", "branco", AttributeResolutionStatus.Resolved, 2, "color", "Cor", "Enum", "branco", 20, "white", "Branco", 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("tamanho", "41", AttributeResolutionStatus.Resolved, 3, "size", "Tamanho", "Text", "41", null, null, null, 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("uso", "corrida", AttributeResolutionStatus.Resolved, 4, "usage", "Uso", "Text", "corrida", null, null, null, 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("estado", "novo", AttributeResolutionStatus.Resolved, 5, "condition", "Condição", "Enum", "NEW", 30, "NEW", "Novo", 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("peso para entrega", "2 kg", AttributeResolutionStatus.Resolved, 6, "shipping_weight", "Peso para entrega", "Measurement", "2 kg", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("2 kg", MeasurementValue: 2m, UnitCode: "kg")
                }
            ],
            AllResolved: true);

        var (orchestrator, intentRewriter, categoryResolver, attributeResolver) = CreateSut(intentResult, categoryResult, attributesResult);

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Equal(1, intentRewriter.CallCount);
        Assert.Equal("sapatos esportivos para corrida", categoryResolver.LastRequest!.CategorySearchQuery);

        Assert.Equal(187, result.Category!.GoogleCategoryId);
        Assert.NotEqual(1065, result.Category!.GoogleCategoryId);

        var forwardedHints = attributeResolver.LastRequest!.AttributeHints;
        Assert.Equal(6, forwardedHints.Count);
        Assert.Contains(forwardedHints, h => h.RawName == "uso" && h.RawValue == "corrida");
        Assert.Contains(forwardedHints, h => h.RawName == "estado" && h.RawValue == "novo");
        Assert.Contains(forwardedHints, h => h.RawName == "peso para entrega" && h.RawValue == "2 kg");

        var shippingWeight = result.Attributes!.Attributes.Single(a => a.AttributeCode == "shipping_weight");
        Assert.Equal(2m, shippingWeight.TypedValue!.MeasurementValue);
        Assert.Equal("kg", shippingWeight.TypedValue.UnitCode);

        var condition = result.Attributes!.Attributes.Single(a => a.AttributeCode == "condition");
        Assert.Equal("NEW", condition.OptionCode);

        Assert.True(result.Attributes!.AllResolved);
        Assert.True(result.ReadyForProposal);
        Assert.Equal(CatalogIntentResolutionStatus.Resolved, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_MicrophoneCompoundAttributesRegression_DecomposesPackageDimensionsAndResolvesConnectionType()
    {
        // Regression for compound attribute extraction: package dimensions
        // (length/width/height) and shipping weight must each arrive as a
        // separate, atomic attributeHint (never a single aggregated
        // "dimensões da embalagem" hint), and connection_type/USB must resolve
        // via the dedicated Enum attribute (never as a Json product_detail).
        var intentResult = new IntentRewriteResult(
            OriginalInput: "Quero cadastrar um microfone condensador USB preto, com corpo de alumínio, produto novo, indicado para podcasts e gravações em estúdio. O peso para entrega é 850 g e a embalagem mede 25 cm de comprimento, 15 cm de largura e 10 cm de altura.",
            NormalizedQuery: "Quero cadastrar um microfone condensador USB preto, com corpo de alumínio, produto novo, indicado para podcasts e gravações em estúdio. O peso para entrega é 850 g e a embalagem mede 25 cm de comprimento, 15 cm de largura e 10 cm de altura.",
            SemanticQuery: "microfone condensador USB preto com corpo de alumínio, produto novo, para podcasts e gravações em estúdio, peso para entrega de 850 g e embalagem de 25 cm de comprimento, 15 cm de largura e 10 cm de altura",
            Intent: CatalogIntent.ProductCreation,
            DetectedLanguage: "pt",
            TargetLocale: "pt-BR",
            CategoryHint: "microfone condensador USB",
            AttributeHints:
            [
                new AttributeHint("tipo", "condensador"),
                new AttributeHint("tipo de conexão", "USB"),
                new AttributeHint("cor", "preto"),
                new AttributeHint("material", "alumínio"),
                new AttributeHint("estado", "novo"),
                new AttributeHint("uso", "podcasts e gravações em estúdio"),
                new AttributeHint("peso para entrega", "850 g"),
                new AttributeHint("comprimento da embalagem", "25 cm"),
                new AttributeHint("largura da embalagem", "15 cm"),
                new AttributeHint("altura da embalagem", "10 cm")
            ],
            SearchTerms: ["microfone", "condensador", "usb", "preto"],
            Confidence: 0.9m,
            CategorySearchQuery: "microfones");

        var categoryResult = new ResolveGoogleCategoryResult(
            "microfone condensador USB",
            GoogleCategoryResolutionStatus.Resolved,
            9999,
            "Microfones",
            "Eletrônicos > Áudio > Microfones",
            3,
            0.65,
            [],
            null,
            ResolutionStrategy.VectorOnly,
            null,
            null);

        var attributesResult = new ResolveAttributeHintsResult(
            [
                new ResolvedAttributeHint("tipo", "condensador", AttributeResolutionStatus.Resolved, 70, "microphone_type", "Tipo de microfone", "Text", "condensador", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("condensador", TextValue: "condensador")
                },
                new ResolvedAttributeHint("tipo de conexão", "USB", AttributeResolutionStatus.Resolved, 69, "connection_type", "Tipo de conexão", "Enum", "USB", 1901, "USB", "USB", 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("cor", "preto", AttributeResolutionStatus.Resolved, 14, "color", "Cor", "Text", "preto", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("preto", TextValue: "preto")
                },
                new ResolvedAttributeHint("material", "alumínio", AttributeResolutionStatus.Resolved, 18, "material", "Material", "Text", "alumínio", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("alumínio", TextValue: "alumínio")
                },
                new ResolvedAttributeHint("estado", "novo", AttributeResolutionStatus.Resolved, 23, "condition", "Condição", "Enum", "NEW", 1201, "NEW", "Novo", 1.0, 1.0, null, [], null),
                new ResolvedAttributeHint("uso", "podcasts e gravações em estúdio", AttributeResolutionStatus.Resolved, 66, "occasion", "Ocasião", "Text", "podcasts e gravações em estúdio", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("podcasts e gravações em estúdio", TextValue: "podcasts e gravações em estúdio")
                },
                new ResolvedAttributeHint("peso para entrega", "850 g", AttributeResolutionStatus.Resolved, 37, "shipping_weight", "Peso para frete", "Measurement", "850 g", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("850 g", MeasurementValue: 850m, UnitCode: "g")
                },
                new ResolvedAttributeHint("comprimento da embalagem", "25 cm", AttributeResolutionStatus.Resolved, 38, "shipping_length", "Comprimento da embalagem", "Measurement", "25 cm", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("25 cm", MeasurementValue: 25m, UnitCode: "cm")
                },
                new ResolvedAttributeHint("largura da embalagem", "15 cm", AttributeResolutionStatus.Resolved, 39, "shipping_width", "Largura da embalagem", "Measurement", "15 cm", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("15 cm", MeasurementValue: 15m, UnitCode: "cm")
                },
                new ResolvedAttributeHint("altura da embalagem", "10 cm", AttributeResolutionStatus.Resolved, 40, "shipping_height", "Altura da embalagem", "Measurement", "10 cm", null, null, null, 1.0, 1.0, null, [], null)
                {
                    TypedValue = new ResolvedAttributeValue("10 cm", MeasurementValue: 10m, UnitCode: "cm")
                }
            ],
            AllResolved: true);

        var (orchestrator, intentRewriter, categoryResolver, attributeResolver) = CreateSut(intentResult, categoryResult, attributesResult);

        var result = await orchestrator.ResolveAsync(new CatalogIntentResolutionRequest("input"), CancellationToken.None);

        Assert.Equal(1, intentRewriter.CallCount);
        Assert.Equal("microfones", categoryResolver.LastRequest!.CategorySearchQuery);

        var forwardedHints = attributeResolver.LastRequest!.AttributeHints;
        Assert.Equal(10, forwardedHints.Count);

        // Package dimensions must be three distinct hints, never aggregated.
        Assert.Contains(forwardedHints, h => h.RawName == "comprimento da embalagem" && h.RawValue == "25 cm");
        Assert.Contains(forwardedHints, h => h.RawName == "largura da embalagem" && h.RawValue == "15 cm");
        Assert.Contains(forwardedHints, h => h.RawName == "altura da embalagem" && h.RawValue == "10 cm");
        Assert.DoesNotContain(forwardedHints, h => h.RawName.Contains("dimensões", StringComparison.OrdinalIgnoreCase));

        // Explicit technical qualifier "condensador" must be extracted as its
        // own attributeHint even though it is also present in categoryHint and
        // semanticQuery (facts must not be deduplicated across fields).
        Assert.Contains(forwardedHints, h => h.RawName == "tipo" && h.RawValue == "condensador");
        Assert.Contains("condensador", intentResult.CategoryHint);
        Assert.Contains("condensador", intentResult.SemanticQuery);

        var microphoneType = result.Attributes!.Attributes.Single(a => a.AttributeCode == "microphone_type");
        Assert.Equal("condensador", microphoneType.TypedValue!.TextValue);

        var connection = result.Attributes!.Attributes.Single(a => a.AttributeCode == "connection_type");
        Assert.Equal("USB", connection.OptionCode);

        var shippingWeight = result.Attributes!.Attributes.Single(a => a.AttributeCode == "shipping_weight");
        Assert.Equal(850m, shippingWeight.TypedValue!.MeasurementValue);
        Assert.Equal("g", shippingWeight.TypedValue.UnitCode);

        var length = result.Attributes!.Attributes.Single(a => a.AttributeCode == "shipping_length");
        var width = result.Attributes!.Attributes.Single(a => a.AttributeCode == "shipping_width");
        var height = result.Attributes!.Attributes.Single(a => a.AttributeCode == "shipping_height");
        Assert.Equal(25m, length.TypedValue!.MeasurementValue);
        Assert.Equal(15m, width.TypedValue!.MeasurementValue);
        Assert.Equal(10m, height.TypedValue!.MeasurementValue);

        Assert.True(result.Attributes!.AllResolved);
        Assert.True(result.ReadyForProposal);
        Assert.Equal(CatalogIntentResolutionStatus.Resolved, result.Status);
    }
}
