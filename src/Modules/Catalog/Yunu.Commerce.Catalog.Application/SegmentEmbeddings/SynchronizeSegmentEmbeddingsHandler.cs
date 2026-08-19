using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Orchestrates a full Segment embedding synchronization: read active Segment
/// Definitions and active Segment Options (SQL Server) → build deterministic
/// pt-BR semantic documents → upsert every active source into the pgvector
/// projection (creating new rows, refreshing content hashes, reactivating
/// rows that became active again and invalidating stale vectors) →
/// deactivate projections that are no longer active → read only pending rows
/// (missing embedding, stale content hash, or a different provider) →
/// generate embeddings (AI module) → complete each row optimistically (docs
/// task: "Implementar sincronização de embeddings de segmentos"). Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeEmbeddings.SynchronizeAttributeEmbeddingsHandler"/>.
///
/// Business orchestration only. Never references Azure, the OpenAI SDK,
/// Npgsql, Pgvector types or raw SQL directly.
///
/// Scope: only SegmentDefinition and SegmentOption are synchronized into
/// public.segment_embeddings. Product/Sku assignments, the canonical taxonomy
/// projection and vector search/reranking are never touched by this handler.
/// </summary>
public sealed class SynchronizeSegmentEmbeddingsHandler
{
    private const string DefinitionEntityType = "SegmentDefinition";
    private const string OptionEntityType = "SegmentOption";
    private const string DefinitionSource = "YunuCommerce.Catalog.SegmentDefinitions";
    private const string OptionSource = "YunuCommerce.Catalog.SegmentOptions";

    private readonly ISegmentEmbeddingSourceRepository _sourceRepository;
    private readonly ISegmentEmbeddingRepository _embeddingRepository;
    private readonly EmbeddingOrchestrator _embeddingOrchestrator;
    private readonly ISegmentEmbeddingSynchronizationGuard _synchronizationGuard;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly SegmentEmbeddingsSyncOptions _syncOptions;
    private readonly ILogger<SynchronizeSegmentEmbeddingsHandler> _logger;

    public SynchronizeSegmentEmbeddingsHandler(
        ISegmentEmbeddingSourceRepository sourceRepository,
        ISegmentEmbeddingRepository embeddingRepository,
        EmbeddingOrchestrator embeddingOrchestrator,
        ISegmentEmbeddingSynchronizationGuard synchronizationGuard,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<SegmentEmbeddingsSyncOptions> syncOptions,
        ILogger<SynchronizeSegmentEmbeddingsHandler> logger)
    {
        _sourceRepository = sourceRepository;
        _embeddingRepository = embeddingRepository;
        _embeddingOrchestrator = embeddingOrchestrator;
        _synchronizationGuard = synchronizationGuard;
        _embeddingOptions = embeddingOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task<SynchronizeSegmentEmbeddingsResult> HandleAsync(
        SynchronizeSegmentEmbeddingsCommand command,
        CancellationToken cancellationToken)
    {
        using var lockToken = _synchronizationGuard.TryAcquire();

        if (lockToken is null)
        {
            throw new SegmentEmbeddingSynchronizationInProgressException();
        }

        var provider = string.IsNullOrWhiteSpace(command.Provider)
            ? _embeddingOptions.DefaultProvider
            : command.Provider;

        var batchSize = command.BatchSize is > 0 ? command.BatchSize.Value : _syncOptions.BatchSize;
        var maxDegreeOfParallelism = _syncOptions.MaxDegreeOfParallelism;
        var locale = _syncOptions.Locale;

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var definitions = await _sourceRepository.GetActiveDefinitionsAsync(cancellationToken);
        var options = await _sourceRepository.GetActiveOptionsAsync(cancellationToken);

        _logger.LogInformation(
            "Segment embedding synchronization started. Provider={Provider} DefinitionsRead={DefinitionsRead} OptionsRead={OptionsRead} BatchSize={BatchSize} MaxDegreeOfParallelism={MaxDegreeOfParallelism}",
            provider,
            definitions.Count,
            options.Count,
            batchSize,
            maxDegreeOfParallelism);

        var sources = new List<SegmentEmbeddingSource>(definitions.Count + options.Count);

        foreach (var definition in definitions)
        {
            var semanticText = SegmentSemanticDocumentBuilder.BuildDefinitionText(definition);
            var entityId = SegmentSemanticDocumentBuilder.BuildDefinitionEntityId(definition.SegmentDefinitionId);
            var metadata = JsonSerializer.Serialize(new
            {
                selectionMode = definition.SelectionMode,
                isRequired = definition.IsRequired,
                source = DefinitionSource
            });

            sources.Add(new SegmentEmbeddingSource
            {
                EntityType = DefinitionEntityType,
                EntityId = entityId,
                SegmentDefinitionId = definition.SegmentDefinitionId,
                SegmentOptionId = null,
                SegmentCode = definition.Code,
                OptionCode = null,
                AssignmentScope = definition.AssignmentScope,
                Locale = locale,
                Name = definition.Name,
                SemanticText = semanticText,
                Metadata = metadata,
                SourceUpdatedAt = definition.UpdatedAt
            });
        }

        foreach (var option in options)
        {
            var semanticText = SegmentSemanticDocumentBuilder.BuildOptionText(option);
            var entityId = SegmentSemanticDocumentBuilder.BuildOptionEntityId(option.SegmentOptionId);
            var metadata = JsonSerializer.Serialize(new
            {
                displayOrder = option.DisplayOrder,
                source = OptionSource
            });

            sources.Add(new SegmentEmbeddingSource
            {
                EntityType = OptionEntityType,
                EntityId = entityId,
                SegmentDefinitionId = option.SegmentDefinitionId,
                SegmentOptionId = option.SegmentOptionId,
                SegmentCode = option.SegmentCode,
                OptionCode = option.OptionCode,
                AssignmentScope = option.AssignmentScope,
                Locale = locale,
                Name = option.OptionName,
                SemanticText = semanticText,
                Metadata = metadata,
                SourceUpdatedAt = option.UpdatedAt
            });
        }

        var existingKeys = await _embeddingRepository.GetExistingKeysAsync(locale, cancellationToken);
        var existingKeySet = new HashSet<(string EntityType, long EntityId)>(existingKeys);

        foreach (var source in sources)
        {
            await _embeddingRepository.UpsertSourceAsync(source, cancellationToken);
        }

        var activeKeys = sources
            .Select(s => (s.EntityType, s.EntityId))
            .ToArray();

        var deactivated = await _embeddingRepository.DeactivateMissingAsync(locale, activeKeys, cancellationToken);

        var pendingItems = await _embeddingRepository.GetPendingAsync(locale, provider, cancellationToken);

        _logger.LogInformation(
            "Segment embedding pending items: {PendingCount} Deactivated={Deactivated}",
            pendingItems.Count,
            deactivated);

        var generated = 0;
        var updated = 0;
        var failed = 0;
        string? resolvedModel = null;

        var batches = pendingItems
            .Select((item, index) => (item, index))
            .GroupBy(x => x.index / batchSize, x => x.item)
            .ToArray();

        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = batches[batchIndex].ToArray();

            _logger.LogInformation(
                "Processing Segment embedding batch {BatchNumber}/{TotalBatches}",
                batchIndex + 1,
                batches.Length);

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                async (item, itemCancellationToken) =>
                {
                    var wasAlreadyPersisted = existingKeySet.Contains((item.EntityType, item.EntityId));

                    try
                    {
                        var embeddingResult = await _embeddingOrchestrator.GenerateAsync(
                            item.SemanticText,
                            provider,
                            itemCancellationToken);

                        resolvedModel = embeddingResult.Model;

                        var completed = await _embeddingRepository.CompleteAsync(
                            item.EntityType,
                            item.EntityId,
                            locale,
                            item.ContentHash,
                            embeddingResult.Provider,
                            embeddingResult.Model,
                            embeddingResult.Embedding,
                            itemCancellationToken);

                        if (!completed)
                        {
                            Interlocked.Increment(ref failed);

                            _logger.LogWarning(
                                "Optimistic completion rejected for Segment entity {EntityType}/{EntityId}: content changed while the embedding was being generated. Item remains pending for the next run.",
                                item.EntityType,
                                item.EntityId);

                            return;
                        }

                        if (wasAlreadyPersisted)
                        {
                            Interlocked.Increment(ref updated);
                        }
                        else
                        {
                            Interlocked.Increment(ref generated);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Interlocked.Increment(ref failed);

                        _logger.LogError(
                            ex,
                            "Failed to generate/persist embedding for Segment entity {EntityType}/{EntityId}",
                            item.EntityType,
                            item.EntityId);
                    }
                });

            _logger.LogInformation(
                "Progress: Generated={Generated} Updated={Updated} Failed={Failed}",
                generated,
                updated,
                failed);
        }

        var totalRead = definitions.Count + options.Count;
        var skipped = totalRead - pendingItems.Count;

        stopwatch.Stop();

        _logger.LogInformation(
            "Segment embedding synchronization completed. Provider={Provider} DefinitionsRead={DefinitionsRead} OptionsRead={OptionsRead} Generated={Generated} Updated={Updated} Skipped={Skipped} Deactivated={Deactivated} Failed={Failed} DurationMs={DurationMs}",
            provider,
            definitions.Count,
            options.Count,
            generated,
            updated,
            skipped,
            deactivated,
            failed,
            stopwatch.ElapsedMilliseconds);

        return new SynchronizeSegmentEmbeddingsResult
        {
            Provider = provider,
            Model = resolvedModel ?? "n/a",
            DefinitionsRead = definitions.Count,
            OptionsRead = options.Count,
            Generated = generated,
            Updated = updated,
            Skipped = skipped,
            Deactivated = deactivated,
            Failed = failed,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
}
