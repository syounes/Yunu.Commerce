using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.Reranking;

namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Deterministic, hybrid resolver of a textual Google category hint (docs
/// task: "Google Category Resolution" + "Contextual candidate reranking"):
/// exact match first, then semantic search (pgvector) + SQL Server
/// validation, then LLM contextual reranking (<see cref="ICandidateReranker"/>)
/// to disambiguate candidates that are lexically close but semantically
/// different (e.g. "running shoes" vs. "Sporting Goods"), then
/// confidence/margin decision. Never persists anything and never invents an
/// id: every accepted result is hydrated and validated against SQL Server
/// (<see cref="IGoogleCategoryCatalogReader"/>) before being returned, and
/// the reranker only ever selects among those already-validated candidates
/// by index; it never receives or returns an official GoogleCategoryId.
/// Mirrors the structure of <see
/// cref="Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeHintResolver"/>.
///
/// Explicitly resolves the "CategoryEmbedding" logical model via <see
/// cref="IAIModelCatalog"/> instead of relying on <see
/// cref="EmbeddingOrchestrator"/>'s default provider, so the same model that
/// generated the stored Google Taxonomy category embeddings is always used.
/// </summary>
public sealed class GoogleCategoryResolver : IGoogleCategoryResolver
{
    private readonly EmbeddingOrchestrator _embeddingOrchestrator;
    private readonly IAIModelCatalog _modelCatalog;
    private readonly IGoogleCategorySemanticSearch _semanticSearch;
    private readonly IGoogleCategoryCatalogReader _catalogReader;
    private readonly ICandidateReranker _reranker;
    private readonly CategoryResolutionOptions _options;
    private readonly RerankingOptions _rerankingOptions;
    private readonly ILogger<GoogleCategoryResolver> _logger;

    public GoogleCategoryResolver(
        EmbeddingOrchestrator embeddingOrchestrator,
        IAIModelCatalog modelCatalog,
        IGoogleCategorySemanticSearch semanticSearch,
        IGoogleCategoryCatalogReader catalogReader,
        ICandidateReranker reranker,
        IOptions<CategoryResolutionOptions> options,
        IOptions<RerankingOptions> rerankingOptions,
        ILogger<GoogleCategoryResolver> logger)
    {
        _embeddingOrchestrator = embeddingOrchestrator;
        _modelCatalog = modelCatalog;
        _semanticSearch = semanticSearch;
        _catalogReader = catalogReader;
        _reranker = reranker;
        _options = options.Value;
        _rerankingOptions = rerankingOptions.Value;
        _logger = logger;
    }

    public async Task<ResolveGoogleCategoryResult> ResolveAsync(
        ResolveGoogleCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.RawCategoryHint))
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.NotFound,
                null, null, null, null, null,
                [],
                "No category hint was provided.",
                null, null, null);
        }

        // Fail fast if "CategoryEmbedding" (or whatever is configured) is not
        // a properly registered Embedding model.
        var resolvedModel = _modelCatalog.Resolve(_options.EmbeddingModel, AIModelType.Embedding);

        // Etapa 1: exact/normalized match in SQL Server.
        var exactMatchStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exactMatches = await _catalogReader.FindExactMatchesAsync(
            request.RawCategoryHint,
            request.Locale,
            cancellationToken);
        exactMatchStopwatch.Stop();

        if (exactMatches.Count == 1)
        {
            var entry = exactMatches[0];

            stopwatch.Stop();

            _logger.LogInformation(
                "Google category resolution for hint {RawCategoryHint} resolved by exact match to {GoogleCategoryId} in {TotalMs}ms",
                request.RawCategoryHint,
                entry.GoogleCategoryId,
                stopwatch.ElapsedMilliseconds);

            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.Resolved,
                entry.GoogleCategoryId,
                entry.Name,
                entry.FullPath,
                entry.Level,
                1.0,
                [new GoogleCategoryCandidate(entry.GoogleCategoryId, entry.Name, entry.FullPath, entry.Level, 1.0)],
                null,
                ResolutionStrategy.ExactMatch,
                null,
                null);
        }

        if (exactMatches.Count > 1)
        {
            // The same name/path can legitimately exist under different
            // branches (e.g. "Acessórios"): do not guess, let semantic search
            // + margin decide, or report Ambiguous.
            _logger.LogInformation(
                "Google category exact match for hint {RawCategoryHint} found {Count} candidates in different branches; falling back to semantic resolution.",
                request.RawCategoryHint,
                exactMatches.Count);
        }

        // Etapa 2: semantic search using categoryHint + semanticQuery context.
        var embeddingStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var queryText = BuildSemanticCategoryText(request.RawCategoryHint, request.SemanticQuery);
        var embeddingResult = await _embeddingOrchestrator.GenerateAsync(queryText, cancellationToken: cancellationToken);

        ValidateEmbeddingModel(embeddingResult, resolvedModel);

        embeddingStopwatch.Stop();

        var pgvectorStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var semanticCandidates = await _semanticSearch.SearchAsync(
            embeddingResult.Embedding,
            _options.TopK,
            request.Locale,
            cancellationToken);

        pgvectorStopwatch.Stop();

        // Hydrate every candidate id in a single batched SQL Server query
        // instead of one query per candidate.
        var sqlServerStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var candidateIds = semanticCandidates.Select(c => c.GoogleCategoryId).Distinct().ToArray();
        var hydratedEntries = await _catalogReader.GetByIdsAsync(candidateIds, cancellationToken);
        var hydratedById = hydratedEntries.ToDictionary(e => e.GoogleCategoryId);

        sqlServerStopwatch.Stop();

        // Only candidates confirmed active in SQL Server count towards
        // threshold/margin decisions; a stale or inactive pgvector row is
        // never trusted.
        var validCandidates = semanticCandidates
            .Where(c => hydratedById.ContainsKey(c.GoogleCategoryId))
            .OrderByDescending(c => c.Similarity)
            .ToArray();

        var candidateDtos = validCandidates
            .Select(c => new GoogleCategoryCandidate(
                c.GoogleCategoryId,
                hydratedById[c.GoogleCategoryId].Name,
                hydratedById[c.GoogleCategoryId].FullPath,
                hydratedById[c.GoogleCategoryId].Level,
                c.Similarity))
            .ToArray();

        var responseCandidates = _options.IncludeCandidatesInResponse ? candidateDtos : [];

        stopwatch.Stop();

        _logger.LogInformation(
            "Google category semantic search for hint {RawCategoryHint}: {CandidateCount} candidates, top scores: {Scores}. " +
            "Model={Model} TopK={TopK} MinimumSimilarity={MinimumSimilarity} Margin={Margin} " +
            "TotalMs={TotalMs} EmbeddingMs={EmbeddingMs} PgvectorMs={PgvectorMs} SqlServerMs={SqlServerMs}",
            request.RawCategoryHint,
            candidateDtos.Length,
            string.Join(", ", candidateDtos.Take(3).Select(c => c.Similarity.ToString("F4"))),
            resolvedModel.DeploymentName,
            _options.TopK,
            _options.MinimumSimilarity,
            _options.MinimumScoreMargin,
            stopwatch.ElapsedMilliseconds,
            embeddingStopwatch.ElapsedMilliseconds,
            pgvectorStopwatch.ElapsedMilliseconds,
            sqlServerStopwatch.ElapsedMilliseconds);

        if (candidateDtos.Length == 0)
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.NotFound,
                null, null, null, null, null,
                responseCandidates,
                "No semantic candidate could be validated against SQL Server.",
                null, null, null);
        }

        var top = candidateDtos[0];

        if (!_rerankingOptions.AlwaysRerankSemanticMatches)
        {
            return ResolveByVectorThresholdOnly(request, candidateDtos, responseCandidates);
        }
        // Etapa 3 (docs task "Contextual candidate reranking"): the vector
        // similarity alone is a recall signal, not a precision decision (e.g.
        // "running shoes" ranking "Sporting Goods" above "Shoes"). Only
        // SQL-Server-validated candidates are ever sent to the reranker, and
        // only by Index + DisplayText + Metadata; GoogleCategoryId is never
        // exposed to the LLM.
        var rerankCandidates = candidateDtos
            .Take(_rerankingOptions.MaximumCandidates)
            .Select((c, index) => new RerankCandidate(
                index,
                c.CategoryName,
                $"Path: {c.CategoryPath}\nVector similarity: {c.Similarity:F4}"))
            .ToArray();

        var rerankRequest = new CandidateRerankRequest(
            Task: "Select the Google product category that describes what the product is.",
            Query: request.RawCategoryHint,
            Context: request.SemanticQuery,
            Candidates: rerankCandidates,
            Locale: request.Locale);

        try
        {
            var rerankStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var rerankResult = await _reranker.RerankAsync(rerankRequest, cancellationToken);
            rerankStopwatch.Stop();

            _logger.LogInformation(
                "Google category reranking for hint {RawCategoryHint}: Decision={Decision} Confidence={Confidence} " +
                "CandidateCount={CandidateCount} RerankMs={RerankMs}",
                request.RawCategoryHint,
                rerankResult.Decision,
                rerankResult.Confidence,
                rerankCandidates.Length,
                rerankStopwatch.ElapsedMilliseconds);

            return BuildRerankedResult(request, candidateDtos, responseCandidates, rerankResult);
        }
        catch (CandidateRerankException ex)
        {
            _logger.LogWarning(
                ex,
                "Google category reranking technical failure for hint {RawCategoryHint}: {Reason}. Falling back to vector threshold.",
                request.RawCategoryHint,
                ex.Reason);

            var fallback = ResolveByVectorThresholdOnly(request, candidateDtos, responseCandidates);

            return fallback.Status == GoogleCategoryResolutionStatus.Resolved
                ? fallback with { Strategy = ResolutionStrategy.VectorFallback }
                : fallback;
        }
    }

    private ResolveGoogleCategoryResult ResolveByVectorThresholdOnly(
        ResolveGoogleCategoryRequest request,
        IReadOnlyList<GoogleCategoryCandidate> candidateDtos,
        IReadOnlyList<GoogleCategoryCandidate> responseCandidates)
    {
        var top = candidateDtos[0];

        if (top.Similarity < _options.MinimumSimilarity)
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.NotFound,
                null, null, null, null,
                top.Similarity,
                responseCandidates,
                "Best candidate is below the minimum similarity threshold.",
                null, null, null);
        }

        if (candidateDtos.Count > 1)
        {
            var second = candidateDtos[1];
            var margin = top.Similarity - second.Similarity;

            if (margin < _options.MinimumScoreMargin)
            {
                return new ResolveGoogleCategoryResult(
                    request.RawCategoryHint,
                    GoogleCategoryResolutionStatus.Ambiguous,
                    null, null, null, null,
                    top.Similarity,
                    responseCandidates,
                    "Top candidates are too close to safely disambiguate.",
                    null, null, null);
            }
        }

        return new ResolveGoogleCategoryResult(
            request.RawCategoryHint,
            GoogleCategoryResolutionStatus.Resolved,
            top.GoogleCategoryId,
            top.CategoryName,
            top.CategoryPath,
            top.Depth,
            top.Similarity,
            responseCandidates,
            null,
            ResolutionStrategy.VectorOnly,
            null,
            null);
    }

    /// <summary>
    /// Converts a validated <see cref="CandidateRerankResult"/> into the
    /// final resolution outcome. The reranker's <c>SelectedCandidateIndex</c>
    /// is deterministically mapped back to the already-validated
    /// <see cref="GoogleCategoryCandidate"/> at that position in
    /// <paramref name="candidateDtos"/> (the same list/order sent to the
    /// reranker); the official <see cref="GoogleCategoryCandidate.GoogleCategoryId"/>
    /// always comes from that candidate, never from the LLM. Confidence and
    /// margin thresholds from <see cref="RerankingOptions"/> are applied on
    /// top of a "Selected" decision before accepting it as Resolved (docs
    /// task: "Contextual candidate reranking" §13); a reranker "Ambiguous" or
    /// "None" decision is never silently overridden by falling back to the
    /// vector Top 1.
    /// </summary>
    private ResolveGoogleCategoryResult BuildRerankedResult(
        ResolveGoogleCategoryRequest request,
        IReadOnlyList<GoogleCategoryCandidate> candidateDtos,
        IReadOnlyList<GoogleCategoryCandidate> responseCandidates,
        CandidateRerankResult rerankResult)
    {
        if (rerankResult.Decision == CandidateRerankDecision.None)
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.NotFound,
                null, null, null, null,
                candidateDtos[0].Similarity,
                responseCandidates,
                rerankResult.Reason,
                ResolutionStrategy.Reranked,
                rerankResult.Confidence,
                rerankResult.Reason);
        }

        if (rerankResult.Decision == CandidateRerankDecision.Ambiguous)
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.Ambiguous,
                null, null, null, null,
                candidateDtos[0].Similarity,
                responseCandidates,
                rerankResult.Reason,
                ResolutionStrategy.Reranked,
                rerankResult.Confidence,
                rerankResult.Reason);
        }

        // Decision == Selected: SelectedCandidateIndex was already validated
        // by the reranker adapter against the request's candidate indices.
        var selectedCandidate = candidateDtos[rerankResult.SelectedCandidateIndex!.Value];

        if (rerankResult.Confidence < _rerankingOptions.MinimumConfidence)
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.Ambiguous,
                null, null, null, null,
                selectedCandidate.Similarity,
                responseCandidates,
                "Reranker confidence is below the minimum required confidence.",
                ResolutionStrategy.Reranked,
                rerankResult.Confidence,
                rerankResult.Reason);
        }

        if (rerankResult.Ranking.Count > 1)
        {
            var orderedRanking = rerankResult.Ranking.OrderByDescending(r => r.RelevanceScore).ToArray();
            var margin = orderedRanking[0].RelevanceScore - orderedRanking[1].RelevanceScore;

            if (margin < _rerankingOptions.MinimumScoreMargin)
            {
                return new ResolveGoogleCategoryResult(
                    request.RawCategoryHint,
                    GoogleCategoryResolutionStatus.Ambiguous,
                    null, null, null, null,
                    selectedCandidate.Similarity,
                    responseCandidates,
                    "Reranker relevance scores are too close to safely disambiguate.",
                    ResolutionStrategy.Reranked,
                    rerankResult.Confidence,
                    rerankResult.Reason);
            }
        }

        return new ResolveGoogleCategoryResult(
            request.RawCategoryHint,
            GoogleCategoryResolutionStatus.Resolved,
            selectedCandidate.GoogleCategoryId,
            selectedCandidate.CategoryName,
            selectedCandidate.CategoryPath,
            selectedCandidate.Depth,
            selectedCandidate.Similarity,
            responseCandidates,
            rerankResult.Reason,
            ResolutionStrategy.Reranked,
            rerankResult.Confidence,
            rerankResult.Reason);
    }

    /// <summary>
    /// Composes the text sent for embedding: preserves the raw category hint
    /// and adds the semantic query as product context, so the embedding is
    /// never generated from a bare generic word alone (e.g. "tênis"). Ignores
    /// empty context and never invents data; deterministic and testable.
    /// </summary>
    public static string BuildSemanticCategoryText(string rawCategoryHint, string? semanticQuery)
    {
        var hint = rawCategoryHint.Trim();

        return string.IsNullOrWhiteSpace(semanticQuery)
            ? $"Categoria de produto sugerida: {hint}."
            : $"Categoria de produto sugerida: {hint}. Contexto do produto: {semanticQuery.Trim()}.";
    }

    private static void ValidateEmbeddingModel(EmbeddingResult embeddingResult, ResolvedAIModel resolvedModel)
    {
        if (!string.Equals(embeddingResult.Model, resolvedModel.DeploymentName, StringComparison.Ordinal))
        {
            throw new CategoryResolutionEmbeddingModelMismatchException(resolvedModel.DeploymentName, embeddingResult.Model);
        }
    }
}
