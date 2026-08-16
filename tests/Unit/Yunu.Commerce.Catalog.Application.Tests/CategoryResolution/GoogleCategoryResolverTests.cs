using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.Reranking;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

/// <summary>
/// Unit tests for GoogleCategoryResolver (docs task: "Google Category
/// Resolution" + "Contextual candidate reranking"). Never touches Azure,
/// pgvector or SQL Server: all dependencies are fakes.
/// </summary>
public sealed class GoogleCategoryResolverTests
{
    private const string EmbeddingModelName = "CategoryEmbedding";
    private const string DeploymentName = "yunu-embedding-category-v1";

    private static (
        GoogleCategoryResolver Resolver,
        FakeGoogleCategoryCatalogReader CatalogReader,
        FakeGoogleCategorySemanticSearch SemanticSearch,
        FakeEmbeddingProvider EmbeddingProvider,
        FakeCandidateReranker Reranker)
        CreateSut(
            double minimumSimilarity = 0.70,
            double minimumScoreMargin = 0.03,
            int topK = 5,
            bool alwaysRerankSemanticMatches = false,
            FakeCandidateReranker? reranker = null)
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

        var resolver = new GoogleCategoryResolver(
            embeddingOrchestrator,
            modelCatalog,
            semanticSearch,
            catalogReader,
            candidateReranker,
            resolutionOptions,
            rerankingOptions,
            NullLogger<GoogleCategoryResolver>.Instance);

        return (resolver, catalogReader, semanticSearch, embeddingProvider, candidateReranker);
    }

    [Fact]
    public async Task ResolveAsync_ExactSingleMatch_ReturnsResolved()
    {
        var (resolver, catalogReader, semanticSearch, embeddingProvider, _) = CreateSut();

        catalogReader.Add(new GoogleCategoryCatalogEntry(123, "Calçados esportivos", "Vestuário > Calçados > Calçados esportivos", 4, true, true));

        var result = await resolver.ResolveAsync(
            new ResolveGoogleCategoryRequest("Calçados esportivos", "tênis masculino branco"),
            CancellationToken.None);

        Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
        Assert.Equal(123, result.GoogleCategoryId);
        Assert.Equal(1.0, result.Similarity);
        Assert.Equal(ResolutionStrategy.ExactMatch, result.Strategy);
        Assert.Equal(0, embeddingProvider.CallCount);
        Assert.Equal(0, semanticSearch.CallCount);
    }

    [Fact]
    public async Task ResolveAsync_DuplicateNameInDifferentBranches_FallsBackToSemanticSearch()
    {
        var (resolver, catalogReader, semanticSearch, _, _) = CreateSut();

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
        var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(minimumSimilarity: 0.70);

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
        var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(minimumSimilarity: 0.70, minimumScoreMargin: 0.05);

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
        var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(minimumSimilarity: 0.70);

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
        var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(minimumSimilarity: 0.10, minimumScoreMargin: 0.50);

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
        var (resolver, _, semanticSearch, embeddingProvider, _) = CreateSut();

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
        var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(minimumSimilarity: 0.70);

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
                var (resolver, catalogReader, semanticSearch, embeddingProvider, _) = CreateSut(minimumSimilarity: 0.70);

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

            var rerankingOptions = Options.Create(new RerankingOptions
            {
                Model = "CatalogReranker",
                MinimumConfidence = 0.75,
                MinimumScoreMargin = 0.10,
                MaximumCandidates = 10,
                AlwaysRerankSemanticMatches = false,
                MaxConcurrentRerankRequests = 4,
                TechnicalFailureFallback = TechnicalFailureFallbackStrategy.VectorThreshold
            });

            var candidateReranker = FakeCandidateReranker.Returning(
                new CandidateRerankResult(CandidateRerankDecision.None, null, 0, [], "unused"));

            var resolver = new GoogleCategoryResolver(
                embeddingOrchestrator,
                modelCatalog,
                semanticSearch,
                catalogReader,
                candidateReranker,
                resolutionOptions,
                rerankingOptions,
                NullLogger<GoogleCategoryResolver>.Instance);

            await Assert.ThrowsAsync<CategoryResolutionEmbeddingModelMismatchException>(() =>
                resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", "contexto"), CancellationToken.None));
        }

        [Fact]
        public async Task ResolveAsync_Cancellation_PropagatesToken()
        {
            var (resolver, catalogReader, _, _, _) = CreateSut();

            catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Exact match short-circuits before any cancellable I/O in this fake
            // setup; the resolver must still accept the token without throwing
            // for the synchronous fake path.
            var result = await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("Categoria A", null), cts.Token);

            Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
        }

        [Fact]
        public async Task ResolveAsync_VectorOnly_MarksStrategyAsVectorOnly()
        {
            var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(minimumSimilarity: 0.70, alwaysRerankSemanticMatches: false);

            catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));
            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria A", 0.80));

            var result = await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", null), CancellationToken.None);

            Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
            Assert.Equal(ResolutionStrategy.VectorOnly, result.Strategy);
        }

        [Fact]
        public async Task ResolveAsync_RunningShoesExample_RerankerSelectsShoesOverTopVectorCandidate()
        {
            // Reproduces the documented failure: the vector Top 1 is "Sporting
            // Goods" (0.4392), but "Shoes" (index 2, 0.4058) is what the product
            // actually is; the reranker must be able to override the vector
            // ranking by selecting a lower-similarity, semantically correct index.
            var rerankResult = new CandidateRerankResult(
                CandidateRerankDecision.Selected,
                SelectedCandidateIndex: 2,
                Confidence: 0.96,
                Ranking:
                [
                    new RerankedCandidateScore(2, 0.96),
                    new RerankedCandidateScore(1, 0.31),
                    new RerankedCandidateScore(0, 0.18)
                ],
                Reason: "The item is footwear intended for running, not sporting equipment.");

            var reranker = FakeCandidateReranker.Returning(rerankResult);

            var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(
                minimumSimilarity: 0.10,
                minimumScoreMargin: 0.50,
                alwaysRerankSemanticMatches: true,
                reranker: reranker);

            catalogReader.Add(new GoogleCategoryCatalogEntry(187, "Shoes", "Apparel & Accessories > Shoes", 2, true, true));
            catalogReader.Add(new GoogleCategoryCatalogEntry(2, "Athletics", "Sporting Goods > Athletics", 2, true, true));
            catalogReader.Add(new GoogleCategoryCatalogEntry(3, "Sporting Goods", "Sporting Goods", 1, true, true));

            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(3, "Sporting Goods", 0.4392));
            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(2, "Athletics", 0.4225));
            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(187, "Shoes", 0.4058));

            var result = await resolver.ResolveAsync(
                new ResolveGoogleCategoryRequest("running shoes", "athletic footwear for road running"),
                CancellationToken.None);

            Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
            Assert.Equal(187, result.GoogleCategoryId);
            Assert.Equal("Shoes", result.CategoryName);
            Assert.Equal(ResolutionStrategy.Reranked, result.Strategy);
            Assert.Equal(0.96, result.RerankConfidence);
            Assert.NotNull(result.Similarity);

            // The reranker must never see the official GoogleCategoryId.
            Assert.NotNull(reranker.LastRequest);
            Assert.All(reranker.LastRequest!.Candidates, c => Assert.DoesNotContain("187", c.DisplayText));
        }

        [Fact]
        public async Task ResolveAsync_RerankerReturnsAmbiguous_ReturnsAmbiguousWithoutFallingBackToTop1()
        {
            var rerankResult = new CandidateRerankResult(
                CandidateRerankDecision.Ambiguous,
                SelectedCandidateIndex: null,
                Confidence: 0.4,
                Ranking: [new RerankedCandidateScore(0, 0.5), new RerankedCandidateScore(1, 0.48)],
                Reason: "Both candidates are plausible.");

            var reranker = FakeCandidateReranker.Returning(rerankResult);
;

            var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(
                minimumSimilarity: 0.10,
                minimumScoreMargin: 0.50,
                alwaysRerankSemanticMatches: true,
                reranker: reranker);

            catalogReader.Add(new GoogleCategoryCatalogEntry(1, "A", "A", 1, true, true));
            catalogReader.Add(new GoogleCategoryCatalogEntry(2, "B", "B", 1, true, true));

            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "A", 0.80));
            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(2, "B", 0.79));

            var result = await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", null), CancellationToken.None);

            Assert.Equal(GoogleCategoryResolutionStatus.Ambiguous, result.Status);
            Assert.Null(result.GoogleCategoryId);
            Assert.Equal(ResolutionStrategy.Reranked, result.Strategy);
        }

        [Fact]
        public async Task ResolveAsync_RerankerReturnsNone_ReturnsNotFound()
        {
            var rerankResult = new CandidateRerankResult(
                CandidateRerankDecision.None,
                SelectedCandidateIndex: null,
                Confidence: 0.1,
                Ranking: [],
                Reason: "No candidate represents the item.");

            var reranker = FakeCandidateReranker.Returning(rerankResult);

            var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(
                minimumSimilarity: 0.10,
                minimumScoreMargin: 0.50,
                alwaysRerankSemanticMatches: true,
                reranker: reranker);

            catalogReader.Add(new GoogleCategoryCatalogEntry(1, "A", "A", 1, true, true));
            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "A", 0.80));

            var result = await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", null), CancellationToken.None);

            Assert.Equal(GoogleCategoryResolutionStatus.NotFound, result.Status);
            Assert.Null(result.GoogleCategoryId);
        }

        [Fact]
        public async Task ResolveAsync_ExactMatch_NeverCallsReranker()
        {
            var reranker = FakeCandidateReranker.Returning(
                new CandidateRerankResult(CandidateRerankDecision.None, null, 0, [], "unused"));

            var (resolver, catalogReader, _, _, _) = CreateSut(alwaysRerankSemanticMatches: true, reranker: reranker);

            catalogReader.Add(new GoogleCategoryCatalogEntry(123, "Calçados esportivos", "Vestuário > Calçados > Calçados esportivos", 4, true, true));

            var result = await resolver.ResolveAsync(
                new ResolveGoogleCategoryRequest("Calçados esportivos", "tênis masculino branco"),
                CancellationToken.None);

            Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
            Assert.Equal(ResolutionStrategy.ExactMatch, result.Strategy);
            Assert.Equal(0, reranker.CallCount);
        }

        [Fact]
        public async Task ResolveAsync_RerankerTechnicalFailure_FallsBackToVectorThreshold()
        {
            var reranker = FakeCandidateReranker.Throwing(
                new CandidateRerankException(CandidateRerankFailureReason.Timeout, "simulated timeout"));

            var (resolver, catalogReader, semanticSearch, _, _) = CreateSut(
                minimumSimilarity: 0.70,
                minimumScoreMargin: 0.03,
                alwaysRerankSemanticMatches: true,
                reranker: reranker);

            catalogReader.Add(new GoogleCategoryCatalogEntry(1, "Categoria A", "Categoria A", 1, true, true));
            semanticSearch.AddCandidate(new GoogleCategorySemanticCandidate(1, "Categoria A", 0.80));

            var result = await resolver.ResolveAsync(new ResolveGoogleCategoryRequest("categoria", null), CancellationToken.None);

            Assert.Equal(GoogleCategoryResolutionStatus.Resolved, result.Status);
            Assert.Equal(1, result.GoogleCategoryId);
            Assert.Equal(ResolutionStrategy.VectorFallback, result.Strategy);
        }
    }
