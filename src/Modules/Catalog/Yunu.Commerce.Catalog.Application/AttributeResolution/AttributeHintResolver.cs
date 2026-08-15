using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Deterministic, hybrid resolver of textual attribute hints (docs task:
/// "Semantic attribute hint resolution"): exact match first (Etapa B), then
/// semantic search + SQL Server validation (Etapa C/D), then free-value
/// validation (Etapa E). Never persists anything and never invents IDs/codes:
/// every accepted result is hydrated and validated against SQL Server
/// (<see cref="IAttributeCatalogReader"/>) before being returned.
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
    private readonly AttributeResolutionOptions _options;
    private readonly ILogger<AttributeHintResolver> _logger;

    public AttributeHintResolver(
        EmbeddingOrchestrator embeddingOrchestrator,
        IAIModelCatalog modelCatalog,
        IAttributeSemanticSearch semanticSearch,
        IAttributeCatalogReader catalogReader,
        IOptions<AttributeResolutionOptions> options,
        ILogger<AttributeHintResolver> logger)
    {
        _embeddingOrchestrator = embeddingOrchestrator;
        _modelCatalog = modelCatalog;
        _semanticSearch = semanticSearch;
        _catalogReader = catalogReader;
        _options = options.Value;
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

            var top = validCandidates[0];

            if (top.Similarity < _options.DefinitionMinimumSimilarity)
            {
                results[index] = NotFoundResult(hint, candidateDtos, "Best candidate is below the minimum similarity threshold.");
                continue;
            }

            if (validCandidates.Length > 1)
            {
                var second = validCandidates[1];
                var margin = top.Similarity - second.Similarity;

                if (margin < _options.MinimumScoreMargin)
                {
                    results[index] = AmbiguousResult(hint, candidateDtos, "Top candidates are too close to safely disambiguate.");
                    continue;
                }
            }

            var resolvedDefinition = hydratedByCode[top.AttributeCode];

            results[index] = await BuildDefinitionResolvedHintAsync(
                hint,
                resolvedDefinition,
                top.Similarity,
                candidateDtos,
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

    private async Task<ResolvedAttributeHint> BuildDefinitionResolvedHintAsync(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> candidates,
        long? googleCategoryId,
        string locale,
        CancellationToken cancellationToken)
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
                cancellationToken);
        }

        if (!AttributeValueValidator.TryNormalize(definition.DataType, hint.RawValue, out var normalizedValue))
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
                $"rawValue '{hint.RawValue}' could not be interpreted as {definition.DataType}.");
        }

        return new ResolvedAttributeHint(
            hint.RawName,
            hint.RawValue,
            AttributeResolutionStatus.Resolved,
            definition.AttributeDefinitionId,
            definition.Code,
            definition.Name,
            definition.DataType,
            normalizedValue,
            null,
            null,
            null,
            definitionSimilarity,
            null,
            requirementLevel,
            filteredCandidates,
            null);
    }

    private async Task<ResolvedAttributeHint> ResolveEnumValueAsync(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        string locale,
        CancellationToken cancellationToken)
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
                null);
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
                optionCandidates: []);
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
                reason: "No active options found for this attribute.");
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
                reason: "No semantic candidate for this option could be validated in SQL Server.");
        }

        var top = validOptions[0];

        if (top.Similarity < _options.OptionMinimumSimilarity)
        {
            return DefinitionResolvedButOptionNotFound(
                hint, definition, definitionSimilarity, definitionCandidates, requirementLevel,
                valueSimilarity: top.Similarity,
                optionCandidates: optionCandidateDtos,
                reason: "Best option candidate is below the minimum similarity threshold.");
        }

        if (validOptions.Length > 1)
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
                    optionCandidateDtos);
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
            optionCandidateDtos);
    }

    private static ResolvedAttributeHint BuildEnumResolvedHint(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        AttributeOptionCatalogEntry option,
        double valueSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        IReadOnlyList<AttributeOptionCandidate> optionCandidates)
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
            optionCandidates);
    }

    private static ResolvedAttributeHint DefinitionResolvedButOptionNotFound(
        AttributeHint hint,
        AttributeDefinitionCatalogEntry definition,
        double definitionSimilarity,
        IReadOnlyList<AttributeCandidate> definitionCandidates,
        AttributeRequirementLevel? requirementLevel,
        double? valueSimilarity,
        IReadOnlyList<AttributeOptionCandidate> optionCandidates,
        string reason)
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
            optionCandidates);
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
