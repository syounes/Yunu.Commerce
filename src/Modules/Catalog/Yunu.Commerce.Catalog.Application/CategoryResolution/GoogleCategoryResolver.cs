using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Deterministic, hybrid resolver of a textual Google category hint (docs
/// task: "Google Category Resolution"): exact match first, then semantic
/// search (pgvector) + SQL Server validation, then threshold/margin
/// decision. Never persists anything and never invents an id: every accepted
/// result is hydrated and validated against SQL Server (<see
/// cref="IGoogleCategoryCatalogReader"/>) before being returned. Mirrors the
/// structure of <see
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
    private readonly CategoryResolutionOptions _options;
    private readonly ILogger<GoogleCategoryResolver> _logger;

    public GoogleCategoryResolver(
        EmbeddingOrchestrator embeddingOrchestrator,
        IAIModelCatalog modelCatalog,
        IGoogleCategorySemanticSearch semanticSearch,
        IGoogleCategoryCatalogReader catalogReader,
        IOptions<CategoryResolutionOptions> options,
        ILogger<GoogleCategoryResolver> logger)
    {
        _embeddingOrchestrator = embeddingOrchestrator;
        _modelCatalog = modelCatalog;
        _semanticSearch = semanticSearch;
        _catalogReader = catalogReader;
        _options = options.Value;
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
                "No category hint was provided.");
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
                "No semantic candidate could be validated against SQL Server.");
        }

        var top = candidateDtos[0];

        if (top.Similarity < _options.MinimumSimilarity)
        {
            return new ResolveGoogleCategoryResult(
                request.RawCategoryHint,
                GoogleCategoryResolutionStatus.NotFound,
                null, null, null, null,
                top.Similarity,
                responseCandidates,
                "Best candidate is below the minimum similarity threshold.");
        }

        if (candidateDtos.Length > 1)
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
                    "Top candidates are too close to safely disambiguate.");
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
            null);
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
