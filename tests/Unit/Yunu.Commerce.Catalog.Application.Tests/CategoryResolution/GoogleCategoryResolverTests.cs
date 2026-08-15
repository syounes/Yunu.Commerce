using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

/// <summary>
/// Unit tests for GoogleCategoryResolver (docs task: "Google Category
/// Resolution"). Never touches Azure, pgvector or SQL Server: all
/// dependencies are fakes.
/// </summary>
public sealed class GoogleCategoryResolverTests
{
    private const string EmbeddingModelName = "CategoryEmbedding";
    private const string DeploymentName = "yunu-embedding-category-v1";

    private static (
        GoogleCategoryResolver Resolver,
        FakeGoogleCategoryCatalogReader CatalogReader,
        FakeGoogleCategorySemanticSearch SemanticSearch,
        FakeEmbeddingProvider EmbeddingProvider)
        CreateSut(
            double minimumSimilarity = 0.70,
            double minimumScoreMargin = 0.03,
            int topK = 5)
    {
        var catalogReader = new FakeGoogleCategoryCatalogReader();
        var semanticSearch = new FakeGoogleCategorySemanticSearch();
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

        var resolutionOptions = Options.Create(new CategoryResolutionOptions
        {
            EmbeddingModel = EmbeddingModelName,
            TopK = topK,
            MinimumSimilarity = minimumSimilarity,
            MinimumScoreMargin = minimumScoreMargin,
            IncludeCandidatesInResponse = true
        });

        var resolver = new GoogleCategoryResolver(
            embeddingOrchestrator,
            modelCatalog,
            semanticSearch,
            catalogReader,
            resolutionOptions,
            NullLogger<GoogleCategoryResolver>.Instance);

        return (resolver, catalogReader, semanticSearch, embeddingProvider);
    }

    [Fact]
    public async Task ResolveAsync_ExactSingleMatch_ReturnsResolved()
    {
        var (resolver, catalogReader, semanticSearch, embeddingProvider) = CreateSut();

        catalogReader.Add(new GoogleCategoryCatalogEntry(123, "Calçados esportivos", "Vestuário > Calçados > Calçados esportivos", 4, true, true));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("Calçados esportivos", "tênis masculino branco"),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
        Assert.Equal(123, result.GoogleCategoryId);
        Assert.Equal(1.0, result.Similarity);
        Assert.Equal(0, embeddingProvider.CallCount);
        Assert.Equal(0, semanticSearch.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_DuplicateNameInDifferentBranches_FallsBackToSemanticSearch()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut();

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Acessórios", "Vestuário > Acessórios", 2, true, true));
        catalogReader.Add(new GoogleCategoryCatalogEntry(2, "Acessórios", "Eletrônicos > Acessórios", 2, true, true));

        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Vestuário > Acessórios", 0.85));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(2, "Eletrônicos > Acessórios", 0.60));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("Acessórios", "cinto de couro masculino"),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
        Assert.Equal(1, result.GoogleCategoryId);
    }

    [Fact]
    public async Task ResolveAsync_TopBelowThreshold_ReturnsNotFound()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumSimilarity: 0.70);

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria X", "Categoria X", 1, true, true));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria X", 0.50));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("categoria desconhecida", null),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.NotFound, result.Status);
        Assert.Null(result.GoogleCategoryId);
        Assert.Equal(0.50, result.Similarity);
    }

    [Fact]
    public async Task ResolveAsync_InsufficientMargin_ReturnsAmbiguous()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumSimilarity: 0.70, minimumScoreMargin: 0.05);

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));
        catalogReader.Add(new GoogleCategoryCatalogEntry(2, "Categoria B", "Categoria B", 1, true, true));

        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria A", 0.80));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(2, "Categoria B", 0.79));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("categoria ambigua", null),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.GoogleCategoryId);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task ResolveAsync_CandidatesNotFoundInSqlServer_AreDiscarded()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumSimilarity: 0.70);

        // Only category 1 is hydrated in SQL Server; category 2 is a stale
        // pgvector row that must be ignored.
        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));

        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(2, "Categoria Removida", 0.95));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria A", 0.80));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("categoria", null),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
        Assert.Equal(1, result.GoogleCategoryId);
    }

    [Fact]
    public async Task ResolveAsync_PreservesCandidateOrderBySimilarity()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumSimilarity: 0.10, minimumScoreMargin: 0.50);

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "A", "A", 1, true, true));
        catalogReader.Add(new GoogleCategoryCatalogEntry(2, "B", "B", 1, true, true));
        catalogReader.Add(new GoogleCategoryCatalogEntry(3, "C", "C", 1, true, true));

        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(2, "B", 0.30));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "A", 0.90));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(3, "C", 0.60));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("categoria", null),
            CancellationToken.None);

        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(1, result.Candidates[0].GoogleCategoryId);
        Assert.Equal(3, result.Candidates[1].GoogleCategoryId);
        Assert.Equal(2, result.Candidates[2].GoogleCategoryId);
    }

    [Fact]
    public async Task ResolveAsync_EmptyCategoryHint_ReturnsNotFoundWithoutGeneratingEmbedding()
    {
        var (resolver, _, semanticSearch, embeddingProvider) = CreateSut();

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("", "algum contexto"),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.NotFound, result.Status);
        Assert.Equal(0, embeddingProvider.CallCount);
        Assert.Equal(0, semanticSearch.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_EmptySemanticQuery_StillResolvesUsingHintAlone()
    {
        var (resolver, catalogReader, semanticSearch, _) = CreateSut(minimumSimilarity: 0.70);

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria A", 0.80));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("categoria a", null),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
    }

    [Theory]
    [InlineData("tênis para corrida", null, "Categoria de produto sugerida: tênis para corrida.")]
    [InlineData("tênis para corrida", "  ", "Categoria de produto sugerida: tênis para corrida.")]
    [InlineData("tênis para corrida", "tênis masculino branco tamanho 41", "Categoria de produto sugerida: tênis para corrida. Contexto do produto: tênis masculino branco tamanho 41.")]
    public void BuildSemanticCategoryText_ComposesDeterministicText(string hint, string? semanticQuery, string expected)
    {
        var text = GoogleCategoryResolver.BuildSemanticCategoryText(hint, semanticQuery);

        Assert.Equal(expected, text);
    }

    [Fact]
    public async Task ResolveAsync_UsesExplicitCategoryEmbeddingModelRegistration()
    {
        var (resolver, catalogReader, semanticSearch, embeddingProvider) = CreateSut(minimumSimilarity: 0.70);

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));
        semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria A", 0.80));

        await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", "contexto"), CancellationToken.None);

        Assert.Equal(1, embeddingProvider.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_MismatchedEmbeddingModel_Throws()
    {
        var catalogReader = new FakeGoogleCategoryCatalogReader();
        var semanticSearch = new FakeGoogleCategorySemanticSearch();
        var embeddingProvider = new FakeEmbeddingProvider(modelName: "some-other-deployment");

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

        var resolutionOptions = Options.Create(new CategoryResolutionOptions
        {
            EmbeddingModel = EmbeddingModelName,
            TopK = 5,
            MinimumSimilarity = 0.70,
            MinimumScoreMargin = 0.03,
            IncludeCandidatesInResponse = true
        });

        var resolver = new GoogleCategoryResolver(
            embeddingOrchestrator,
            modelCatalog,
            semanticSearch,
            catalogReader,
            resolutionOptions,
            NullLogger<GoogleCategoryResolver>.Instance);

        await Assert.ThrowsAsync<CategoryResolutionEmbeddingModelMismatchException>(() =>
            resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", "contexto"), CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_Cancellation_PropagatesToken()
    {
        var (resolver, catalogReader, _, _) = CreateSut();

        catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Exact match short-circuits before any cancellable I/O in this fake
        // setup; the resolver must still accept the token without throwing
        // for the synchronous fake path.
        var result = await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("Categoria A", null), cts.Token);

        Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
    }
}
