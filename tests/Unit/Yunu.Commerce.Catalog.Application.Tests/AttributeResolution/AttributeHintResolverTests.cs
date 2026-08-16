using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.AI.Application.Reranking;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeResolution;

/// <summary>
/// Unit tests for AttributeHintResolver (docs task: "Semantic attribute hint
/// resolution" + "Contextual candidate reranking"). Never touches Azure,
/// pgvector or SQL Server: all dependencies are fakes.
/// </summary>
public sealed class AttributeHintResolverTests
{
    private const string EmbeddingModelName = "CategoryEmbedding";
    private const string DeploymentName = "yunu-embedding-category-v1";

    private static (
        AttributeHintResolver Resolver,
        FakeAttributeCatalogReader CatalogReader,
        FakeAttributeSemanticSearch SemanticSearch,
        FakeEmbeddingProvider EmbeddingProvider)
        CreateSut(
            double definitionMinimumSimilarity = 0.75,
            double optionMinimumSimilarity = 0.78,
            double minimumScoreMargin = 0.05,
            int topK = 5,
            bool alwaysRerankSemanticMatches = false,
            FakeCandidateReranker? reranker = null)
    {
        var catalogReader = new FakeAttributeCatalogReader();
        var semanticSearch = new FakeAttributeSemanticSearch();
        var embeddingProvider = new FakeEmbeddingProvider(modelName: DeploymentName);

        var embeddingOrchestrator = new EmbeddingOrchestrator(
            new[] { embeddingProvider },
            Options.Create(new EmbeddingOptions { DefaultProvider = embeddingProvider.Name }));

        var aiOptions = new AIOptions();
        aiOptions.Connections["AzureOpenAI"] = new AIConnectionOptions { Endpoint = "https://example.openai.azure.com/openai/v1/", ApiKey = "test-key" };
        aiOptions.Models[EmbeddingModelName] = new AIModelOptions
        {
            Connection = "AzureOpenAI",
            DeploymentName = DeploymentName,
            ModelType = AIModelType.Embedding,
            Dimensions = 1536
        };

        var modelCatalog = new AIModelCatalog(Options.Create(aiOptions));

        var resolutionOptions = Options.Create(new AttributeResolutionOptions
        {
            EmbeddingModel = EmbeddingModelName,
            TopK = topK,
            DefinitionMinimumSimilarity = definitionMinimumSimilarity,
            OptionMinimumSimilarity = optionMinimumSimilarity,
            MinimumScoreMargin = minimumScoreMargin,
            IncludeCandidatesInResponse = true
        });

        var rerankingOptions = Options.Create(new RerankingOptions
        {
            Model = "CatalogReranker",
            MinimumConfidence = 0.75,
            MinimumScoreMargin = 0.10,
            MaximumCandidates = 10,
            AlwaysRerankSemanticMatches = alwaysRerankSemanticMatches,
            MaxConcurrentRerankRequests = 4,
            TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
        });

        var candidateReranker = reranker ?? FakeCandidateReranker.Returning(
            new CandidateRerankResult(CandidateRerankDecision.None, null, 0, [], "unused"));

        var resolver = new AttributeHintResolver(
            embeddingOrchestrator,
            modelCatalog,
            semanticSearch,
            catalogReader,
            candidateReranker,
            resolutionOptions,
            rerankingOptions,
            NullLogger<AttributeHintResolver>.Instance);

        return (resolver, catalogReader, semanticSearch, embeddingProvider);
    }

    private static AttributeDefinitionCatalogEntry GenderDefinition() => new(
        AttributeDefinitionId: 47,
        Code: "gender",
        Name: "Gênero",
        GoogleAttributeName: "gender",
        DataType: "Enum",
        Cardinality: "Single",
        UnitFamily: null,
        ValidationRegex: null,
        MinNumericValue: null,
        MaxNumericValue: null,
        MaxLength: null,
        IsActive: true);

    private static AttributeDefinitionCatalogEntry ColorDefinition() => new(
        AttributeDefinitionId: 14,
        Code: "color",
        Name: "Cor",
        GoogleAttributeName: "color",
        DataType: "Text",
        Cardinality: "Single",
        UnitFamily: null,
        ValidationRegex: null,
        MinNumericValue: null,
        MaxNumericValue: null,
        MaxLength: 100,
        IsActive: true);

    private static AttributeDefinitionCatalogEntry ConditionDefinition() => new(
        AttributeDefinitionId: 23,
        Code: "condition",
        Name: "Condição",
        GoogleAttributeName: "condition",
        DataType: "Enum",
        Cardinality: "Single",
        UnitFamily: null,
        ValidationRegex: null,
        MinNumericValue: null,
        MaxNumericValue: null,
        MaxLength: null,
        IsActive: true);

    private static AttributeDefinitionCatalogEntry SizeDefinition() => new(
        AttributeDefinitionId: 15,
        Code: "size",
        Name: "Tamanho",
        GoogleAttributeName: null,
        DataType: "Text",
        Cardinality: "Single",
        UnitFamily: null,
        ValidationRegex: null,
        MinNumericValue: null,
        MaxNumericValue: null,
        MaxLength: 100,
        IsActive: true);

    [Fact]
    public async Task Resolves_by_exact_code_match()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("color", "branco")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("color", resolved.AttributeCode);
        Assert.Equal(0, semanticSearch.DefinitionSearchCallCount);
    }

    [Fact]
    public async Task Resolves_by_exact_pt_BR_name_match_accent_insensitive()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("COR", "branco")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("color", resolved.AttributeCode);
    }

    [Fact]
    public async Task Resolves_publico_homem_to_gender_MALE_via_semantic_search()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(GenderDefinition());
        catalogReader.AddOption(new AttributeOptionCatalogEntry(1401, 47, "MALE", "male", "Masculino", true));
        catalogReader.AddOption(new AttributeOptionCatalogEntry(1402, 47, "FEMALE", "female", "Feminino", true));

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.91);
        semanticSearch.AddDefinitionCandidate("age_group", "Faixa etária", 0.78);
        semanticSearch.AddOptionCandidate("gender", "MALE", "Masculino", 0.96);
        semanticSearch.AddOptionCandidate("gender", "FEMALE", "Feminino", 0.40);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("público", "homem")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("gender", resolved.AttributeCode);
        Assert.Equal("MALE", resolved.OptionCode);
        Assert.True(result.AllResolved);
    }

    [Fact]
    public async Task Best_option_below_threshold_exposes_candidates_and_value_similarity()
    {
        // Reproduces the reported público/homem case: gender is resolved
        // (0.91 >= 0.75), but the best MALE option candidate (0.5498) is
        // below OptionMinimumSimilarity (0.78). Observability must still
        // surface the rejected candidate and its score.
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(optionMinimumSimilarity: 0.78);
        catalogReader.AddDefinition(GenderDefinition());
        catalogReader.AddOption(new AttributeOptionCatalogEntry(1401, 47, "MALE", "male", "Masculino", true));

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.91);
        semanticSearch.AddOptionCandidate("gender", "MALE", "Masculino", 0.5498);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("público", "homem")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.NotFound, resolved.Status);
        Assert.Equal("gender", resolved.AttributeCode);
        Assert.Null(resolved.AttributeOptionId);
        Assert.Equal(0.5498, resolved.ValueSimilarity);
        Assert.Equal("Best option candidate is below the minimum similarity threshold.", resolved.Reason);

        var optionCandidate = Assert.Single(resolved.OptionCandidates);
        Assert.Equal(1401, optionCandidate.AttributeOptionId);
        Assert.Equal("MALE", optionCandidate.OptionCode);
        Assert.Equal("Masculino", optionCandidate.OptionName);
        Assert.Equal(0.5498, optionCandidate.Similarity);
    }

    [Fact]
    public async Task Option_candidates_without_sufficient_margin_are_Ambiguous_with_candidates_exposed()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumScoreMargin: 0.05, optionMinimumSimilarity: 0.5);
        catalogReader.AddDefinition(GenderDefinition());
        catalogReader.AddOption(new AttributeOptionCatalogEntry(1401, 47, "MALE", "male", "Masculino", true));
        catalogReader.AddOption(new AttributeOptionCatalogEntry(1402, 47, "FEMALE", "female", "Feminino", true));

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.91);
        semanticSearch.AddOptionCandidate("gender", "MALE", "Masculino", 0.80);
        semanticSearch.AddOptionCandidate("gender", "FEMALE", "Feminino", 0.79);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("público", "homem")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Ambiguous, resolved.Status);
        Assert.Equal(0.80, resolved.ValueSimilarity);
        Assert.Equal(2, resolved.OptionCandidates.Count);
        Assert.Contains("insufficient margin", resolved.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_active_options_found_reports_dedicated_reason()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(GenderDefinition());
        // No options registered/hydrated at all for gender.

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.91);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("público", "homem")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.NotFound, resolved.Status);
        Assert.Equal("No active options found for this attribute.", resolved.Reason);
        Assert.Empty(resolved.OptionCandidates);
    }

    [Fact]
    public async Task Option_candidate_not_validated_in_SQL_Server_reports_dedicated_reason()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(GenderDefinition());
        // pgvector returns a candidate, but it is never hydrated/validated in
        // SQL Server (e.g. stale row or belongs to a different attribute).
        catalogReader.AddOption(new AttributeOptionCatalogEntry(9001, 99, "MALE", "male", "Masculino", true));

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.91);
        semanticSearch.AddOptionCandidate("gender", "MALE", "Masculino", 0.95);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("público", "homem")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.NotFound, resolved.Status);
        Assert.Equal("No semantic candidate for this option could be validated in SQL Server.", resolved.Reason);
        Assert.Empty(resolved.OptionCandidates);
    }

    [Fact]
    public async Task Rejects_option_belonging_to_a_different_attribute()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(GenderDefinition());
        // MALE only exists under a *different* AttributeDefinitionId (99), so
        // it must never be accepted as gender's option even if pgvector
        // (incorrectly) suggested it.
        catalogReader.AddOption(new AttributeOptionCatalogEntry(9001, 99, "MALE", "male", "Masculino", true));

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.91);
        semanticSearch.AddOptionCandidate("gender", "MALE", "Masculino", 0.95);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("público", "homem")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.NotFound, resolved.Status);
        Assert.Null(resolved.AttributeOptionId);
    }

    [Fact]
    public async Task Text_attribute_without_option_is_resolved_as_free_value()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());
        semanticSearch.AddDefinitionCandidate("color", "Cor", 0.9);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("cor de fundo", "branco")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("color", resolved.AttributeCode);
        Assert.Null(resolved.AttributeOptionId);
        Assert.Equal("branco", resolved.NormalizedValue);
    }

    [Fact]
    public async Task Enum_attribute_without_value_is_resolved_without_option()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ConditionDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("condition", null)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Null(resolved.AttributeOptionId);
        Assert.False(result.AllResolved);
    }

    [Fact]
    public async Task Candidate_below_threshold_is_NotFound()
    {
        var (resolver, _, semanticSearch, _) = CreateSut(definitionMinimumSimilarity: 0.75);
        semanticSearch.AddDefinitionCandidate("shipping_weight", "Peso para frete", 0.5);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("peso desconhecido", "2 kg")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.NotFound, resolved.Status);
    }

    [Fact]
    public async Task Candidates_without_sufficient_margin_are_Ambiguous()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumScoreMargin: 0.05);
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            33, "product_weight", "Peso do produto", null, "Measurement", "Single", "Weight", null, 0, 2000, null, true));
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            37, "shipping_weight", "Peso para frete", null, "Measurement", "Single", "Weight", null, 0, null, null, true));

        semanticSearch.AddDefinitionCandidate("product_weight", "Peso do produto", 0.80);
        semanticSearch.AddDefinitionCandidate("shipping_weight", "Peso para frete", 0.79);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("peso para entrega", "2 kg")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Ambiguous, resolved.Status);
    }

    [Fact]
    public async Task No_candidates_result_in_NotFound()
    {
        var (resolver, _, _, _) = CreateSut();

        var request = new ResolveAttributeHintsRequest([new AttributeHint("atributo inexistente", "valor")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.NotFound, resolved.Status);
    }

    [Fact]
    public async Task Invalid_integer_value_is_reported_as_InvalidValue()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            41, "min_handling_time", "Prazo mínimo de manuseio", null, "Integer", "Single", null, null, 0, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("min_handling_time", "não é um número")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
    }

    [Fact]
    public async Task GoogleCategoryId_null_does_not_populate_requirement_level()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("color", "branco")], GoogleCategoryId: null);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Null(result.Attributes[0].RequirementLevel);
    }

    [Fact]
    public async Task GoogleCategoryId_informed_populates_requirement_level_when_rule_exists()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());
        catalogReader.AddRule(new GoogleCategoryAttributeRuleEntry(1000, 14, "Required", true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("color", "branco")], GoogleCategoryId: 1000);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(AttributeRequirementLevel.Required, result.Attributes[0].RequirementLevel);
    }

    [Fact]
    public async Task Preserves_original_hint_order_despite_parallel_semantic_resolution()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());
        catalogReader.AddDefinition(SizeDefinition());

        semanticSearch.AddDefinitionCandidate("color", "Cor", 0.9);
        semanticSearch.AddDefinitionCandidate("size", "Tamanho", 0.9);

        var request = new ResolveAttributeHintsRequest([
            new AttributeHint("cor de fundo", "branco"),
            new AttributeHint("tamanho do produto", "41"),
            new AttributeHint("color", "preto")
        ]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(3, result.Attributes.Count);
        Assert.Equal("cor de fundo", result.Attributes[0].RawName);
        Assert.Equal("tamanho do produto", result.Attributes[1].RawName);
        Assert.Equal("color", result.Attributes[2].RawName);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ResolveAttributeHintsRequest([new AttributeHint("cor desconhecida", "branco")]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(request, cts.Token));
    }

    [Fact]
    public async Task Batch_of_multiple_hints_resolves_each_independently()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(ColorDefinition());
        catalogReader.AddDefinition(GenderDefinition());
        catalogReader.AddOption(new AttributeOptionCatalogEntry(1401, 47, "MALE", "male", "Masculino", true));

        semanticSearch.AddDefinitionCandidate("gender", "Gênero", 0.9);
        semanticSearch.AddOptionCandidate("gender", "MALE", "Masculino", 0.9);

        var request = new ResolveAttributeHintsRequest([
            new AttributeHint("cor", "branco"),
            new AttributeHint("público", "homem")
        ]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(2, result.Attributes.Count);
        Assert.All(result.Attributes, a => Assert.Equal(AttributeResolutionStatus.Resolved, a.Status));
    }

    private static AttributeDefinitionCatalogEntry ShippingWeightDefinition() => new(
        AttributeDefinitionId: 37,
        Code: "shipping_weight",
        Name: "Peso para frete",
        GoogleAttributeName: null,
        DataType: "Measurement",
        Cardinality: "Single",
        UnitFamily: "Weight",
        ValidationRegex: null,
        MinNumericValue: 0,
        MaxNumericValue: null,
        MaxLength: null,
        IsActive: true);

    [Theory]
    [InlineData("2 kg", 2, "kg")]
    [InlineData("2kg", 2, "kg")]
    [InlineData("2,5 kg", 2.5, "kg")]
    [InlineData("2.5 kg", 2.5, "kg")]
    [InlineData("500 g", 500, "g")]
    public async Task Measurement_hint_preserves_typed_value_and_normalized_display(string rawValue, decimal expectedValue, string expectedUnit)
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ShippingWeightDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("shipping_weight", rawValue)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal($"{expectedValue} {expectedUnit}", resolved.NormalizedValue);
        Assert.NotNull(resolved.TypedValue);
        Assert.Equal(expectedValue, resolved.TypedValue!.MeasurementValue);
        Assert.Equal(expectedUnit, resolved.TypedValue.UnitCode);
        Assert.True(result.AllResolved);
    }

    [Theory]
    [InlineData("1,75 m", 1.75, "m")]
    [InlineData("150 cm", 150, "cm")]
    public async Task Measurement_length_hint_preserves_typed_value(string rawValue, decimal expectedValue, string expectedUnit)
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            34, "product_length", "Comprimento do produto", null, "Measurement", "Single", "Length", null, 0, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("product_length", rawValue)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(expectedValue, resolved.TypedValue!.MeasurementValue);
        Assert.Equal(expectedUnit, resolved.TypedValue.UnitCode);
    }

    [Theory]
    [InlineData("250 ml", 250, "ml")]
    [InlineData("1 L", 1, "l")]
    public async Task Measurement_volume_hint_preserves_typed_value(string rawValue, decimal expectedValue, string expectedUnit)
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            22, "capacity", "Capacidade", null, "Measurement", "Single", "Volume", null, 0, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("capacity", rawValue)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(expectedValue, resolved.TypedValue!.MeasurementValue);
        Assert.Equal(expectedUnit, resolved.TypedValue.UnitCode);
    }

    [Theory]
    [InlineData("2 quilos", 2, "kg")]
    [InlineData("500 gramas", 500, "g")]
    public async Task Measurement_alias_is_normalized_to_canonical_unit_code(string rawValue, decimal expectedValue, string expectedUnit)
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ShippingWeightDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("shipping_weight", rawValue)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(expectedUnit, resolved.TypedValue!.UnitCode);
        Assert.Equal(expectedValue, resolved.TypedValue.MeasurementValue);
    }

    [Fact]
    public async Task Measurement_alias_litro_is_normalized_to_l()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            22, "capacity", "Capacidade", null, "Measurement", "Single", "Volume", null, 0, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("capacity", "1 litro")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("l", resolved.TypedValue!.UnitCode);
        Assert.Equal(1, resolved.TypedValue.MeasurementValue);
    }

    [Fact]
    public async Task Measurement_with_incompatible_unit_family_is_InvalidValue()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ShippingWeightDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("shipping_weight", "2 cm")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
        Assert.Contains("incompatible with UnitFamily", resolved.Reason);
        Assert.Null(resolved.TypedValue);
        Assert.False(result.AllResolved);
    }

    [Fact]
    public async Task Measurement_without_unit_is_InvalidValue()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ShippingWeightDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("shipping_weight", "2")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
        Assert.Contains("unit is required", resolved.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.AllResolved);
    }

    [Fact]
    public async Task Measurement_with_invalid_number_is_InvalidValue()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(ShippingWeightDefinition());

        var request = new ResolveAttributeHintsRequest([new AttributeHint("shipping_weight", "dois kg")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
    }

    [Theory]
    [InlineData("R$ 199,90", 199.90, "BRL")]
    [InlineData("BRL 199.90", 199.90, "BRL")]
    [InlineData("199,90 BRL", 199.90, "BRL")]
    [InlineData("US$ 50.00", 50.00, "USD")]
    [InlineData("USD 50", 50, "USD")]
    public async Task Money_hint_preserves_typed_amount_and_currency(string rawValue, decimal expectedAmount, string expectedCurrency)
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            26, "price", "Preço", null, "Money", "Single", null, null, 0, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("price", rawValue)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(expectedAmount, resolved.TypedValue!.MoneyAmount);
        Assert.Equal(expectedCurrency, resolved.TypedValue.CurrencyCode);
    }

    [Theory]
    [InlineData("sim", true)]
    [InlineData("true", true)]
    [InlineData("verdadeiro", true)]
    [InlineData("1", true)]
    [InlineData("não", false)]
    [InlineData("nao", false)]
    [InlineData("false", false)]
    [InlineData("falso", false)]
    [InlineData("0", false)]
    public async Task Boolean_hint_preserves_typed_value(string rawValue, bool expected)
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            31, "is_bundle", "É kit", null, "Boolean", "Single", null, null, null, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("is_bundle", rawValue)]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(expected, resolved.TypedValue!.BooleanValue);
    }

    [Fact]
    public async Task Decimal_hint_accepts_comma_and_dot()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            60, "popularity_rank", "Popularidade", null, "Decimal", "Single", null, null, 0, 100, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("popularity_rank", "2,5")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal(2.5m, resolved.TypedValue!.DecimalValue);
    }

    [Fact]
    public async Task Integer_hint_rejects_fractional_value()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            41, "min_handling_time", "Prazo mínimo de manuseio", null, "Integer", "Single", null, null, 0, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("min_handling_time", "2,5")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
    }

    [Fact]
    public async Task DateTime_hint_preserves_typed_value_from_iso()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            25, "availability_date", "Data de disponibilidade", null, "DateTime", "Single", null, null, null, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("availability_date", "2026-08-20")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.TypedValue!.DateTimeValue);
    }

    [Fact]
    public async Task Json_hint_valid_is_resolved_with_typed_value()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            13, "product_detail", "Detalhe técnico", null, "Json", "Multiple", null, null, null, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("product_detail", "{\"key\":\"value\"}")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("{\"key\":\"value\"}", resolved.TypedValue!.JsonValue);
    }

    [Fact]
    public async Task Json_hint_invalid_is_InvalidValue()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            13, "product_detail", "Detalhe técnico", null, "Json", "Multiple", null, null, null, null, null, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("product_detail", "{not valid json")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
    }

    [Fact]
    public async Task Url_hint_valid_is_resolved()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            8, "link", "Link do produto", null, "Url", "Single", null, null, null, null, 2048, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("link", "https://example.com/p/1")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("https://example.com/p/1", resolved.TypedValue!.TextValue);
    }

    [Fact]
    public async Task Url_hint_invalid_is_InvalidValue()
    {
        var (resolver, catalogReader, _, _) = CreateSut();
        catalogReader.AddDefinition(new AttributeDefinitionCatalogEntry(
            8, "link", "Link do produto", null, "Url", "Single", null, null, null, null, 2048, true));

        var request = new ResolveAttributeHintsRequest([new AttributeHint("link", "not a url")]);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.InvalidValue, resolved.Status);
    }

    [Fact]
    public async Task Regression_peso_para_entrega_2kg_resolves_shipping_weight_with_typed_value()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();
        catalogReader.AddDefinition(ShippingWeightDefinition());

        semanticSearch.AddDefinitionCandidate("shipping_weight", "Peso para frete", 0.91);

        var request = new ResolveAttributeHintsRequest([new AttributeHint("peso para entrega", "2 kg")], GoogleCategoryId: 187);

        var result = await resolver.ResolveAsync(request, CancellationToken.None);

        var resolved = Assert.Single(result.Attributes);
        Assert.Equal(AttributeResolutionStatus.Resolved, resolved.Status);
        Assert.Equal("shipping_weight", resolved.AttributeCode);
        Assert.Equal(2, resolved.TypedValue!.MeasurementValue);
        Assert.Equal("kg", resolved.TypedValue.UnitCode);
        Assert.Equal("2 kg", resolved.NormalizedValue);
        Assert.True(result.AllResolved);
    }
}
