using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.AI.Application.Reranking;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Deterministic, hybrid resolver of textual attribute hints (docs task:
/// "Semantic attribute hint resolution" + "Contextual candidate reranking"):
/// exact match first (Etapa B, no reranking), then semantic search + SQL
/// Server validation (Etapa C/D) + LLM contextual reranking (<see
/// cref="ICandidateReranker"/>) for both attribute definitions and, for Enum
/// attributes, attribute options, then free-value validation (Etapa E). Never
/// persists anything and never invents IDs/codes: every accepted result is
/// hydrated and validated against SQL Server (<see
/// cref="IAttributeCatalogReader"/>) before being returned, and the reranker
/// only ever selects among those already-validated candidates by index; it
/// never receives or returns an official AttributeDefinitionId, AttributeCode,
/// AttributeOptionId or OptionCode.
///
/// Explicitly resolves the "CategoryEmbedding" logical model (docs
/// authorization item 3) via <see cref="IAIModelCatalog"/> instead of relying
/// on <see cref="EmbeddingOrchestrator"/>'s default provider, so a future
/// registration of additional embedding models does not silently change which
/// vectors are compared against the stored attribute embeddings.
/// </summary>
public sealed class AttributeHintResolver : IAttributeHintResolver
{
    private readonly EmbeddingOrchestrator _embeddingOrchestrator;
    private readonly IAIModelCatalog _modelCatalog;
    private readonly IAttributeSemanticSearch _semanticSearch;
    private readonly IAttributeCatalogReader _catalogReader;
    private readonly ICandidateReranker _reranker;
    private readonly AttributeResolutionOptions _options;
    private readonly RerankingOptions _rerankingOptions;
    private readonly ILogger<AttributeHintResolver> _logger;

    public AttributeHintResolver(
        EmbeddingOrchestrator embeddingOrchestrator,
        IAIModelCatalog modelCatalog,
        IAttributeSemanticSearch semanticSearch,
        IAttributeCatalogReader catalogReader,
        ICandidateReranker reranker,
        IOptions<AttributeResolutionOptions> options,
        IOptions<RerankingOptions> rerankingOptions,
        ILogger<AttributeHintResolver> logger)
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

    public async Task<ResolveAttributeHintsResult> ResolveAsync(
        ResolveAttributeHintsRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Fail fast if "CategoryEmbedding" (or whatever is configured) is not
        // a properly registered Embedding model; also gives us the expected
        // deployment name to validate against what the provider actually used.
        var resolvedModel = _modelCatalog.Resolve(_options.EmbeddingModel, AIModelType.Embedding);

        var hints = request.AttributeHints;
        var results = new ResolvedAttributeHint?[hints.Count];
        var normalizedNames = new string[hints.Count];

        for (var i = 0; i < hints.Count; i++)
        {
            normalizedNames[i] = AttributeHintNormalizer.Normalize(hints[i].RawName);
        }

        // Etapa B: batched exact match across all hints in one round-trip.
        var exactMatchStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var exactDefinitions = await _catalogReader.FindDefinitionsByExactMatchAsync(
            normalizedNames.Distinct().ToArray(),
            cancellationToken);
        exactMatchStopwatch.Stop();

        var exactByNormalizedValue = BuildExactDefinitionLookup(exactDefinitions);

        var pendingSemanticIndices = new List<int>();

        for (var i = 0; i < hints.Count; i++)
        {
            if (exactByNormalizedValue.TryGetValue(normalizedNames[i], out var exactDefinition))
            {
                results[i] = await BuildDefinitionResolvedHintAsync(
                    hints[i],
                    exactDefinition,
                    definitionSimilarity: 1.0,
                    candidates: [],
                    request.GoogleCategoryId,
                    request.Locale,
                    cancellationToken);
            }
            else
            {
                pendingSemanticIndices.Add(i);
            }
        }

        // Etapa C: semantic resolution for hints without an exact match.
        var embeddingStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var pgvectorStopwatch = new System.Diagnostics.Stopwatch();

        var semanticCandidatesByIndex = new ConcurrentDictionary<int, IReadOnlyList<SemanticAttributeCandidate>>();

        await Parallel.ForEachAsync(
            pendingSemanticIndices,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (index, itemCancellationToken) =>
            {
                var hint = hints[index];
                var queryText = BuildDefinitionQueryText(hint);

                var embeddingResult = await _embeddingOrchestrator.GenerateAsync(queryText, cancellationToken: itemCancellationToken);

                ValidateEmbeddingModel(embeddingResult, resolvedModel);

                var candidates = await _semanticSearch.SearchDefinitionsAsync(
                    embeddingResult.Embedding,
                    _options.TopK,
                    request.Locale,
                    itemCancellationToken);

                semanticCandidatesByIndex[index] = candidates;
            });

        embeddingStopwatch.Stop();

        // Hydrate every distinct candidate code returned by pgvector in a
        // single batched SQL Server query instead of one query per candidate.
        pgvectorStopwatch.Start();

        var allCandidateCodes = semanticCandidatesByIndex.Values
            .SelectMany(c => c.Select(x => x.AttributeCode))
            .Distinct()
            .ToArray();

        var hydratedDefinitions = await _catalogReader.GetDefinitionsByCodesAsync(allCandidateCodes, cancellationToken);
        var hydratedByCode = hydratedDefinitions.ToDictionary(d => d.Code, StringComparer.Ordinal);

        pgvectorStopwatch.Stop();

        foreach (var index in pendingSemanticIndices)
        {
            var hint = hints[index];
            var candidates = semanticCandidatesByIndex.TryGetValue(index, out var found)
                ? found
                : [];

            // Only candidates that are actually active in SQL Server count
            // towards threshold/margin decisions; a stale or inactive pgvector
            // row is never trusted.
            var validCandidates = candidates
                .Where(c => hydratedByCode.ContainsKey(c.AttributeCode))
                .OrderByDescending(c => c.Similarity)
                .ToArray();

            _logger.LogInformation(
                "Attribute definition semantic search for hint {RawName}: {CandidateCount} candidates, top scores: {Scores}",
                hint.RawName,
                validCandidates.Length,
                string.Join(", ", validCandidates.Take(3).Select(c => c.Similarity.ToString("F4"))));

            var candidateDtos = validCandidates
                .Select(c => new AttributeCandidate(
                    hydratedByCode[c.AttributeCode].AttributeDefinitionId,
                    c.AttributeCode,
                    c.Name,
                    c.Similarity))
                .ToArray();

            if (validCandidates.Length == 0)
            {
                results[index] = NotFoundResult(hint, candidateDtos, "No semantic candidate met the minimum similarity threshold.");
                continue;
            }

            results[index] = await ResolveDefinitionFromCandidatesAsync(
                hint,
                candidateDtos,
                hydratedByCode,
                request.GoogleCategoryId,
                request.Locale,
                cancellationToken);
        }

        stopwatch.Stop();

        var resolvedAttributes = results.Select(r => r!).ToArray();

        // For Enum attributes, "fully resolved" also requires a valid option
        // (docs task: "Semantic attribute hint resolution", AllResolved).
        var allResolved = resolvedAttributes.All(r =>
            r.Status == AttributeResolutionStatus.Resolved &&
            (r.DataType != "Enum" || r.AttributeOptionId is not null));

        _logger.LogInformation(
            "Attribute hint resolution completed. Total={Total} Resolved={Resolved} Ambiguous={Ambiguous} NotFound={NotFound} Invalid={Invalid} " +
            "Model={Model} TopK={TopK} DefinitionThreshold={DefinitionThreshold} OptionThreshold={OptionThreshold} Margin={Margin} " +
            "TotalMs={TotalMs} EmbeddingMs={EmbeddingMs} PgvectorMs={PgvectorMs} SqlServerMs={SqlServerMs}",
            resolvedAttributes.Length,
            resolvedAttributes.Count(r => r.Status == AttributeResolutionStatus.Resolved),
            resolvedAttributes.Count(r => r.Status == AttributeResolutionStatus.Ambiguous),
            resolvedAttributes.Count(r => r.Status == AttributeResolutionStatus.NotFound),
            resolvedAttributes.Count(r => r.Status == AttributeResolutionStatus.InvalidValue),
            resolvedModel.DeploymentName,
            _options.TopK,
            _options.DefinitionMinimumSimilarity,
            _options.OptionMinimumSimilarity,
            _options.MinimumScoreMargin,
            stopwatch.ElapsedMilliseconds,
            embeddingStopwatch.ElapsedMilliseconds,
            pgvectorStopwatch.ElapsedMilliseconds,
            exactMatchStopwatch.ElapsedMilliseconds);

        return new ResolveAttributeHintsResult(resolvedAttributes, allResolved);
    }

    /// <summary>
    /// Decides the attribute definition for a hint that did not match
    /// exactly, from its SQL-Server-validated semantic candidates (docs task:
    /// "Contextual candidate reranking" §11). Vector similarity alone is a
    /// recall signal; when reranking is enabled the LLM only ever selects
    /// among these already-validated candidates by index, and
    /// AttributeDefinitionId/AttributeCode are never exposed to it.
    /// </summary>
    private async Task<ResolvedAttributeHint> ResolveDefinitionFromCandidatesAsync(
        AttributeHint hint,
        IReadOnlyList<AttributeCandidate> candidateDtos,
        Dictionary<string, AttributeDefinitionCatalogEntry> hydratedByCode,
        long? googleCategoryId,
        string locale,
        CancellationToken cancellationToken)
    {
        if (!_rerankingOptions.AlwaysRerankSemanticMatches)
        {
            return await ResolveDefinitionByVectorThresholdAsync(
                hint, candidateDtos, hydratedByCode, googleCategoryId, locale, cancellationToken, ResolutionStrategy.VectorOnly);
        }

        var rerankCandidates = candidateDtos
            .Take(_rerankingOptions.MaximumCandidates)
            .Select((c, index) => new RerankCandidate(
                index,
                c.AttributeName,
                $"Vector similarity: {c.Similarity:F4}"))
            .ToArray();

        var rerankRequest = new CandidateRerankRequest(
            Task: "Select the catalog attribute definition that represents the user's field.",
            Query: hint.RawName,
            Context: string.IsNullOrWhiteSpace(hint.RawValue) ? null : $"Value: {hint.RawValue}",
            Candidates: rerankCandidates,
            Locale: locale);

        try
        {
            var rerankResult = await _reranker.RerankAsync(rerankRequest, cancellationToken);

            _logger.LogInformation(
                "Attribute definition reranking for hint {RawName}: Decision={Decision} Confidence={Confidence} CandidateCount={CandidateCount}",
                hint.RawName,
                rerankResult.Decision,
                rerankResult.Confidence,
                rerankCandidates.Length);

            return await BuildDefinitionRerankedResultAsync(
                hint, candidateDtos, hydratedByCode, googleCategoryId, locale, cancellationToken, rerankResult);
        }
        catch (CandidateRerankException ex)
        {
            _logger.LogWarning(
                ex,
                "Attribute definition reranking technical failure for hint {RawName}: {Reason}. Falling back to vector threshold.",
                hint.RawName,
                ex.Reason);

            return await ResolveDefinitionByVectorThresholdAsync(
                hint, candidateDtos, hydratedByCode, googleCategoryId, locale, cancellationToken, ResolutionStrategy.VectorFallback);
        }
    }

    private async Task<ResolvedAttributeHint> ResolveDefinitionByVectorThresholdAsync(
        AttributeHint hint,
        IReadOnlyList<AttributeCandidate> candidateDtos,
        Dictionary<string, AttributeDefinitionCatalogEntry> hydratedByCode,
        long? googleCategoryId,
        string locale,
        CancellationToken cancellationToken,
        ResolutionStrategy strategyWhenResolved)
    {
        var top = candidateDtos[0];

        if (top.Similarity < _options.DefinitionMinimumSimilarity)
        {
            return NotFoundResult(hint, candidateDtos, "Best candidate is below the minimum similarity threshold.");
        }

        if (candidateDtos.Count > 1)
        {
            var second = candidateDtos[1];
            var margin = top.Similarity - second.Similarity;

            if (margin < _options.MinimumScoreMargin)
            {
                return AmbiguousResult(hint, candidateDtos, "Top candidates are too close to safely disambiguate.");
            }
        }

        var resolvedDefinition = hydratedByCode[top.AttributeCode];

        return await BuildDefinitionResolvedHintAsync(
            hint,
            resolvedDefinition,
            top.Similarity,
            candidateDtos,
            googleCategoryId,
            locale,
            cancellationToken,
            strategyWhenResolved,
            null,
            null);
    }

    private async Task<ResolvedAttributeHint> BuildDefinitionRerankedResultAsync(
        AttributeHint hint,
        IReadOnlyList<AttributeCandidate> candidateDtos,
        Dictionary<string, AttributeDefinitionCatalogEntry> hydratedByCode,
        long? googleCategoryId,
        string locale,
        CancellationToken cancellationToken,
        CandidateRerankResult rerankResult)
    {
        if (rerankResult.Decision == CandidateRerankDecision.None)
        {
            return NotFoundResult(hint, candidateDtos, rerankResult.Reason) with
            {
                DefinitionStrategy = ResolutionStrategy.Reranked,
                DefinitionRerankConfidence = rerankResult.Confidence,
                DefinitionRerankReason = rerankResult.Reason
            };
        }

        if (rerankResult.Decision == CandidateRerankDecision.Ambiguous)
        {
            return AmbiguousResult(hint, candidateDtos, rerankResult.Reason) with
            {
                DefinitionStrategy = ResolutionStrategy.Reranked,
                DefinitionRerankConfidence = rerankResult.Confidence,
                DefinitionRerankReason = rerankResult.Reason
            };
        }

        var selectedCandidate = candidateDtos[rerankResult.SelectedCandidateIndex!.Value];

        if (rerankResult.Confidence < _rerankingOptions.MinimumConfidence)
        {
            return AmbiguousResult(hint, candidateDtos, "Reranker confidence is below the minimum required confidence.") with
            {
                DefinitionStrategy = ResolutionStrategy.Reranked,
                DefinitionRerankConfidence = rerankResult.Confidence,
                DefinitionRerankReason = rerankResult.Reason
            };
        }

        // Note: ambiguity/ties are the reranker's own responsibility to
        // declare via Decision == Ambiguous (handled above). An explicit
        // Selected decision with sufficient Confidence must not be silently
        // overridden by independently recomputing a margin over the raw
        // Ranking relevance scores; doing so previously caused a correct,
        // high-confidence reranker selection (e.g. "estado = novo" -> Condição
        // at 0.9 confidence) to be downgraded to Ambiguous.

        var resolvedDefinition = hydratedByCode[selectedCandidate.AttributeCode];

        return await BuildDefinitionResolvedHintAsync(
            hint,
            resolvedDefinition,
            selectedCandidate.Similarity,
            candidateDtos,
            googleCategoryId,
            locale,
            cancellationToken,
            ResolutionStrategy.Reranked,
            rerankResult.Confidence,
            rerankResult.Reason);
    }

    private async Task<ResolvedAttributeHint> BuildDefinitionResolvedHintAsync(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> candidates,
        long? googleCategoryId,
        string locale,
        CancellationToken cancellationToken)
    {
        return await BuildDefinitionResolvedHintAsync(
            hint, definition, definitionSimilarity, candidates, googleCategoryId, locale, cancellationToken,
            ResolutionStrategy.ExactMatch, null, null);
    }

    private async Task<ResolvedAttributeHint> BuildDefinitionResolvedHintAsync(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> candidates,
        long? googleCategoryId,
        string locale,
        CancellationToken cancellationToken,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason)
    {
        AttributeRequirementLevel? requirementLevel = null;

        if (googleCategoryId is not null)
        {
            var rules = await _catalogReader.GetCategoryRulesAsync(
                googleCategoryId.Value,
                [definition.AttributeDefinitionId],
                cancellationToken);

            var rule = rules.FirstOrDefault(r => r.AttributeDefinitionId == definition.AttributeDefinitionId);

            if (rule is not null && Enum.TryParse<AttributeRequirementLevel>(rule.RequirementLevel, out var level))
            {
                requirementLevel = level;
            }
        }

        var filteredCandidates = _options.IncludeCandidatesInResponse ? candidates : [];

        if (definition.DataType == "Enum")
        {
            return await ResolveEnumValueAsync(
                hint,
                definition,
                definitionSimilarity,
                filteredCandidates,
                requirementLevel,
                locale,
                cancellationToken,
                definitionStrategy,
                definitionRerankConfidence,
                definitionRerankReason);
        }

        if (!AttributeValueValidator.TryNormalize(definition, hint.RawValue, out var typedValue, out var invalidReason))
        {
            return new ResolvedAttributeHint(
                hint.RawName,
                hint.RawValue,
                AttributeResolutionStatus.InvalidValue,
                definition.AttributeDefinitionId,
                definition.Code,
                definition.Name,
                definition.DataType,
                null,
                null,
                null,
                null,
                definitionSimilarity,
                null,
                requirementLevel,
                filteredCandidates,
                invalidReason ?? $"rawValue '{hint.RawValue}' could not be interpreted as {definition.DataType}.")
            with
            {
                DefinitionStrategy = definitionStrategy,
                DefinitionRerankConfidence = definitionRerankConfidence,
                DefinitionRerankReason = definitionRerankReason
            };
        }

        return new ResolvedAttributeHint(
            hint.RawName,
            hint.RawValue,
            AttributeResolutionStatus.Resolved,
            definition.AttributeDefinitionId,
            definition.Code,
            definition.Name,
            definition.DataType,
            typedValue?.DisplayValue,
            null,
            null,
            null,
            definitionSimilarity,
            null,
            requirementLevel,
            filteredCandidates,
            null)
        with
        {
            DefinitionStrategy = definitionStrategy,
            DefinitionRerankConfidence = definitionRerankConfidence,
            DefinitionRerankReason = definitionRerankReason,
            TypedValue = typedValue
        };
    }

    private async Task<ResolvedAttributeHint> ResolveEnumValueAsync(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        string locale,
        CancellationToken cancellationToken,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason)
    {
        if (string.IsNullOrWhiteSpace(hint.RawValue))
        {
            // Enum attribute resolved, but no value was provided to resolve
            // an option: the definition itself is still Resolved information,
            // but there is nothing further to validate.
            return new ResolvedAttributeHint(
                hint.RawName,
                hint.RawValue,
                AttributeResolutionStatus.Resolved,
                definition.AttributeDefinitionId,
                definition.Code,
                definition.Name,
                definition.DataType,
                null,
                null,
                null,
                null,
                definitionSimilarity,
                null,
                requirementLevel,
                definitionCandidates,
                null)
            with
            {
                DefinitionStrategy = definitionStrategy,
                DefinitionRerankConfidence = definitionRerankConfidence,
                DefinitionRerankReason = definitionRerankReason
            };
        }

        var normalizedValue = AttributeHintNormalizer.Normalize(hint.RawValue);

        var exactOptions = await _catalogReader.FindOptionsByExactMatchAsync(
            definition.AttributeDefinitionId,
            [normalizedValue],
            cancellationToken);

        var exactOption = exactOptions.FirstOrDefault(o =>
            AttributeHintNormalizer.Normalize(o.Code) == normalizedValue ||
            AttributeHintNormalizer.Normalize(o.Name) == normalizedValue ||
            (o.GoogleValue is not null && AttributeHintNormalizer.Normalize(o.GoogleValue) == normalizedValue));

        if (exactOption is not null)
        {
            return BuildEnumResolvedHint(
                hint,
                definition,
                definitionSimilarity,
                exactOption,
                valueSimilarity: 1.0,
                definitionCandidates,
                requirementLevel,
                optionCandidates: [],
                definitionStrategy,
                definitionRerankConfidence,
                definitionRerankReason,
                optionStrategy: ResolutionStrategy.ExactMatch,
                optionRerankConfidence: null,
                optionRerankReason: null);
        }

        var resolvedModel = _modelCatalog.Resolve(_options.EmbeddingModel, AIModelType.Embedding);
        var queryText = BuildOptionQueryText(hint, definition);

        var embeddingResult = await _embeddingOrchestrator.GenerateAsync(queryText, cancellationToken: cancellationToken);

        ValidateEmbeddingModel(embeddingResult, resolvedModel);

        var semanticOptions = await _semanticSearch.SearchOptionsAsync(
            definition.Code,
            embeddingResult.Embedding,
            _options.TopK,
            locale,
            cancellationToken);

        if (semanticOptions.Count == 0)
        {
            return DefinitionResolvedButOptionNotFound(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                valueSimilarity: null,
                optionCandidates: [],
                reason: "No active options found for this attribute.",
                definitionStrategy, definitionRerankConfidence, definitionRerankReason);
        }

        var candidateCodes = semanticOptions.Select(o => o.OptionCode).Distinct().ToArray();
        var hydratedOptions = await _catalogReader.GetOptionsByCodesAsync(definition.AttributeDefinitionId, candidateCodes, cancellationToken);
        var hydratedByCode = hydratedOptions.ToDictionary(o => o.Code, StringComparer.Ordinal);

        var validOptions = semanticOptions
            .Where(o => hydratedByCode.ContainsKey(o.OptionCode))
            .OrderByDescending(o => o.Similarity)
            .ToArray();

        _logger.LogInformation(
            "Attribute option semantic search for hint {RawName}/{RawValue} (attribute {AttributeCode}): {CandidateCount} candidates, top scores: {Scores}",
            hint.RawName,
            hint.RawValue,
            definition.Code,
            validOptions.Length,
            string.Join(", ", validOptions.Take(3).Select(o => o.Similarity.ToString("F4"))));

        var optionCandidateDtos = _options.IncludeCandidatesInResponse
            ? validOptions
                .Select(o => new AttributeOptionCandidate(
                    hydratedByCode[o.OptionCode].AttributeOptionId,
                    o.OptionCode,
                    o.Name,
                    o.Similarity))
                .ToArray()
            : [];

        if (validOptions.Length == 0)
        {
            return DefinitionResolvedButOptionNotFound(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                valueSimilarity: null,
                optionCandidates: optionCandidateDtos,
                reason: "No semantic candidate for this option could be validated in SQL Server.",
                definitionStrategy, definitionRerankConfidence, definitionRerankReason);
        }

        return await ResolveOptionFromCandidatesAsync(
            hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
            validOptions, hydratedByCode, optionCandidateDtos, locale, cancellationToken,
            definitionStrategy, definitionRerankConfidence, definitionRerankReason);
    }

    /// <summary>
    /// Decides the attribute option for an Enum attribute value that did not
    /// match exactly, from its SQL-Server-validated semantic candidates (docs
    /// task: "Contextual candidate reranking" §12). Only candidates belonging
    /// to the already-resolved parent attribute are ever sent to the
    /// reranker; AttributeOptionId/OptionCode are never exposed to it.
    /// </summary>
    private async Task<ResolvedAttributeHint> ResolveOptionFromCandidatesAsync(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        IReadOnlyList<SemanticAttributeOptionCandidate> validOptions,
        Dictionary<string, AttributeOptionCatalogEntry> hydratedByCode,
        IReadOnlyList<AttributeOptionCandidate> optionCandidateDtos,
        string locale,
        CancellationToken cancellationToken,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason)
    {
        if (!_rerankingOptions.AlwaysRerankSemanticMatches)
        {
            return ResolveOptionByVectorThreshold(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                validOptions, hydratedByCode, optionCandidateDtos,
                definitionStrategy, definitionRerankConfidence, definitionRerankReason,
                ResolutionStrategy.VectorOnly);
        }

        var rerankCandidates = validOptions
            .Take(_rerankingOptions.MaximumCandidates)
            .Select((o, index) => new RerankCandidate(
                index,
                o.Name,
                $"Vector similarity: {o.Similarity:F4}"))
            .ToArray();

        var rerankRequest = new CandidateRerankRequest(
            Task: $"Select the option of attribute '{definition.Name}' that represents the user's value.",
            Query: hint.RawValue ?? string.Empty,
            Context: null,
            Candidates: rerankCandidates,
            Locale: locale);

        try
        {
            var rerankResult = await _reranker.RerankAsync(rerankRequest, cancellationToken);

            _logger.LogInformation(
                "Attribute option reranking for hint {RawName}/{RawValue} (attribute {AttributeCode}): Decision={Decision} Confidence={Confidence} CandidateCount={CandidateCount}",
                hint.RawName,
                hint.RawValue,
                definition.Code,
                rerankResult.Decision,
                rerankResult.Confidence,
                rerankCandidates.Length);

            return BuildOptionRerankedResult(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                validOptions, hydratedByCode, optionCandidateDtos,
                definitionStrategy, definitionRerankConfidence, definitionRerankReason,
                rerankResult);
        }
        catch (CandidateRerankException ex)
        {
            _logger.LogWarning(
                ex,
                "Attribute option reranking technical failure for hint {RawName}/{RawValue}: {Reason}. Falling back to vector threshold.",
                hint.RawName,
                hint.RawValue,
                ex.Reason);

            return ResolveOptionByVectorThreshold(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                validOptions, hydratedByCode, optionCandidateDtos,
                definitionStrategy, definitionRerankConfidence, definitionRerankReason,
                ResolutionStrategy.VectorFallback);
        }
    }

    private ResolvedAttributeHint ResolveOptionByVectorThreshold(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        IReadOnlyList<SemanticAttributeOptionCandidate> validOptions,
        Dictionary<string, AttributeOptionCatalogEntry> hydratedByCode,
        IReadOnlyList<AttributeOptionCandidate> optionCandidateDtos,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason,
        ResolutionStrategy strategyWhenResolved)
    {
        var top = validOptions[0];

        if (top.Similarity < _options.OptionMinimumSimilarity)
        {
            return DefinitionResolvedButOptionNotFound(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                valueSimilarity: top.Similarity,
                optionCandidates: optionCandidateDtos,
                reason: "Best option candidate is below the minimum similarity threshold.",
                definitionStrategy, definitionRerankConfidence, definitionRerankReason);
        }

        if (validOptions.Count > 1)
        {
            var second = validOptions[1];
            var margin = top.Similarity - second.Similarity;

            if (margin < _options.MinimumScoreMargin)
            {
                return new ResolvedAttributeHint(
                    hint.RawName,
                    hint.RawValue,
                    AttributeResolutionStatus.Ambiguous,
                    definition.AttributeDefinitionId,
                    definition.Code,
                    definition.Name,
                    definition.DataType,
                    null,
                    null,
                    null,
                    null,
                    definitionSimilarity,
                    top.Similarity,
                    requirementLevel,
                    definitionCandidates,
                    "Top option candidates are too close to safely disambiguate (insufficient margin between option candidates).",
                    optionCandidateDtos)
                with
                {
                    DefinitionStrategy = definitionStrategy,
                    DefinitionRerankConfidence = definitionRerankConfidence,
                    DefinitionRerankReason = definitionRerankReason
                };
            }
        }

        var resolvedOption = hydratedByCode[top.OptionCode];

        return BuildEnumResolvedHint(
            hint,
            definition,
            definitionSimilarity,
            resolvedOption,
            top.Similarity,
            definitionCandidates,
            requirementLevel,
            optionCandidateDtos,
            definitionStrategy,
            definitionRerankConfidence,
            definitionRerankReason,
            optionStrategy: strategyWhenResolved,
            optionRerankConfidence: null,
            optionRerankReason: null);
    }

    private ResolvedAttributeHint BuildOptionRerankedResult(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        IReadOnlyList<SemanticAttributeOptionCandidate> validOptions,
        Dictionary<string, AttributeOptionCatalogEntry> hydratedByCode,
        IReadOnlyList<AttributeOptionCandidate> optionCandidateDtos,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason,
        CandidateRerankResult rerankResult)
    {
        if (rerankResult.Decision == CandidateRerankDecision.None)
        {
            return DefinitionResolvedButOptionNotFound(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                valueSimilarity: validOptions[0].Similarity,
                optionCandidates: optionCandidateDtos,
                reason: rerankResult.Reason,
                definitionStrategy, definitionRerankConfidence, definitionRerankReason) with
            {
                OptionStrategy = ResolutionStrategy.Reranked,
                OptionRerankConfidence = rerankResult.Confidence,
                OptionRerankReason = rerankResult.Reason
            };
        }

        if (rerankResult.Decision == CandidateRerankDecision.Ambiguous)
        {
            return new ResolvedAttributeHint(
                hint.RawName,
                hint.RawValue,
                AttributeResolutionStatus.Ambiguous,
                definition.AttributeDefinitionId,
                definition.Code,
                definition.Name,
                definition.DataType,
                null,
                null,
                null,
                null,
                definitionSimilarity,
                validOptions[0].Similarity,
                requirementLevel,
                definitionCandidates,
                rerankResult.Reason,
                optionCandidateDtos)
            with
            {
                DefinitionStrategy = definitionStrategy,
                DefinitionRerankConfidence = definitionRerankConfidence,
                DefinitionRerankReason = definitionRerankReason,
                OptionStrategy = ResolutionStrategy.Reranked,
                OptionRerankConfidence = rerankResult.Confidence,
                OptionRerankReason = rerankResult.Reason
            };
        }

        var selected = validOptions[rerankResult.SelectedCandidateIndex!.Value];

        if (rerankResult.Confidence < _rerankingOptions.MinimumConfidence)
        {
            return new ResolvedAttributeHint(
                hint.RawName,
                hint.RawValue,
                AttributeResolutionStatus.Ambiguous,
                definition.AttributeDefinitionId,
                definition.Code,
                definition.Name,
                definition.DataType,
                null,
                null,
                null,
                null,
                definitionSimilarity,
                selected.Similarity,
                requirementLevel,
                definitionCandidates,
                "Reranker confidence is below the minimum required confidence.",
                optionCandidateDtos)
            with
            {
                DefinitionStrategy = definitionStrategy,
                DefinitionRerankConfidence = definitionRerankConfidence,
                DefinitionRerankReason = definitionRerankReason,
                OptionStrategy = ResolutionStrategy.Reranked,
                OptionRerankConfidence = rerankResult.Confidence,
                OptionRerankReason = rerankResult.Reason
            };
        }

        if (rerankResult.Ranking.Count > 1)
        {
            var orderedRanking = rerankResult.Ranking.OrderByDescending(r => r.RelevanceScore).ToArray();
            var margin = orderedRanking[0].RelevanceScore - orderedRanking[1].RelevanceScore;

            if (margin < _rerankingOptions.MinimumScoreMargin)
            {
                return new ResolvedAttributeHint(
                    hint.RawName,
                    hint.RawValue,
                    AttributeResolutionStatus.Ambiguous,
                    definition.AttributeDefinitionId,
                    definition.Code,
                    definition.Name,
                    definition.DataType,
                    null,
                    null,
                    null,
                    null,
                    definitionSimilarity,
                    selected.Similarity,
                    requirementLevel,
                    definitionCandidates,
                    "Reranker relevance scores are too close to safely disambiguate.",
                    optionCandidateDtos)
                with
                {
                    DefinitionStrategy = definitionStrategy,
                    DefinitionRerankConfidence = definitionRerankConfidence,
                    DefinitionRerankReason = definitionRerankReason,
                    OptionStrategy = ResolutionStrategy.Reranked,
                    OptionRerankConfidence = rerankResult.Confidence,
                    OptionRerankReason = rerankResult.Reason
                };
            }
        }

        var resolvedOption = hydratedByCode[selected.OptionCode];

        return BuildEnumResolvedHint(
            hint,
            definition,
            definitionSimilarity,
            resolvedOption,
            selected.Similarity,
            definitionCandidates,
            requirementLevel,
            optionCandidateDtos,
            definitionStrategy,
            definitionRerankConfidence,
            definitionRerankReason,
            optionStrategy: ResolutionStrategy.Reranked,
            optionRerankConfidence: rerankResult.Confidence,
            optionRerankReason: rerankResult.Reason);
    }

    private static ResolvedAttributeHint BuildEnumResolvedHint(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        AttributeOptionCatalogEntry option,
        double valueSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        IReadOnlyList<AttributeOptionCandidate> optionCandidates,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason,
        ResolutionStrategy optionStrategy,
        double? optionRerankConfidence,
        string? optionRerankReason)
    {
        return new ResolvedAttributeHint(
            hint.RawName,
            hint.RawValue,
            AttributeResolutionStatus.Resolved,
            definition.AttributeDefinitionId,
            definition.Code,
            definition.Name,
            definition.DataType,
            option.Name,
            option.AttributeOptionId,
            option.Code,
            option.Name,
            definitionSimilarity,
            valueSimilarity,
            requirementLevel,
            definitionCandidates,
            null,
            optionCandidates)
        with
        {
            DefinitionStrategy = definitionStrategy,
            DefinitionRerankConfidence = definitionRerankConfidence,
            DefinitionRerankReason = definitionRerankReason,
            OptionStrategy = optionStrategy,
            OptionRerankConfidence = optionRerankConfidence,
            OptionRerankReason = optionRerankReason
        };
    }

    private static ResolvedAttributeHint DefinitionResolvedButOptionNotFound(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        double? valueSimilarity,
        IReadOnlyList<AttributeOptionCandidate> optionCandidates,
        string reason,
        ResolutionStrategy definitionStrategy,
        double? definitionRerankConfidence,
        string? definitionRerankReason)
    {
        // The definition itself was resolved, but the Enum value could not be
        // resolved safely: the overall hint is not ready for persistence, so
        // it is reported as NotFound rather than Resolved. ValueSimilarity
        // still carries the best rejected option's score (when one exists)
        // to help calibrate OptionMinimumSimilarity/MinimumScoreMargin.
        return new ResolvedAttributeHint(
            hint.RawName,
            hint.RawValue,
            AttributeResolutionStatus.NotFound,
            definition.AttributeDefinitionId,
            definition.Code,
            definition.Name,
            definition.DataType,
            null,
            null,
            null,
            null,
            definitionSimilarity,
            valueSimilarity,
            requirementLevel,
            definitionCandidates,
            reason,
            optionCandidates)
        with
        {
            DefinitionStrategy = definitionStrategy,
            DefinitionRerankConfidence = definitionRerankConfidence,
            DefinitionRerankReason = definitionRerankReason
        };
    }

    private static ResolvedAttributeHint NotFoundResult(AttributeHint hint, IReadOnlyList<AttributeCandidate> candidates, string reason) =>
        new(hint.RawName, hint.RawValue, AttributeResolutionStatus.NotFound, null, null, null, null, null, null, null, null, null, null, null, candidates, reason);

    private static ResolvedAttributeHint AmbiguousResult(AttributeHint hint, IReadOnlyList<AttributeCandidate> candidates, string reason) =>
        new(hint.RawName, hint.RawValue, AttributeResolutionStatus.Ambiguous, null, null, null, null, null, null, null, null, null, null, null, candidates, reason);

    private static string BuildDefinitionQueryText(AttributeHint hint) =>
        string.IsNullOrWhiteSpace(hint.RawValue)
            ? $"Atributo informado: {hint.RawName}."
            : $"Atributo informado: {hint.RawName}. Valor informado: {hint.RawValue}.";

    private static string BuildOptionQueryText(AttributeHint hint, AttributeDefinitionCatalogEntry definition) =>
        $"Atributo: {definition.Name}. Valor informado: {hint.RawValue}.";

    private static Dictionary<string, AttributeDefinitionCatalogEntry> BuildExactDefinitionLookup(
        IReadOnlyList<AttributeDefinitionCatalogEntry> definitions)
    {
        var lookup = new Dictionary<string, AttributeDefinitionCatalogEntry>();

        foreach (var definition in definitions)
        {
            lookup.TryAdd(AttributeHintNormalizer.Normalize(definition.Code), definition);
            lookup.TryAdd(AttributeHintNormalizer.Normalize(definition.Name), definition);

            if (!string.IsNullOrWhiteSpace(definition.GoogleAttributeName))
            {
                lookup.TryAdd(AttributeHintNormalizer.Normalize(definition.GoogleAttributeName), definition);
            }
        }

        return lookup;
    }

    private static void ValidateEmbeddingModel(EmbeddingResult embeddingResult, ResolvedAIModel resolvedModel)
    {
        if (!string.Equals(embeddingResult.Model, resolvedModel.DeploymentName, StringComparison.Ordinal))
        {
            throw new AttributeResolutionEmbeddingModelMismatchException(resolvedModel.DeploymentName, embeddingResult.Model);
        }
    }
}
