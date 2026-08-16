using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Orchestrates a full SKU attribute embedding synchronization: read active
/// Attribute Definitions and active Attribute Options (SQL Server) → build
/// deterministic pt-BR semantic documents → skip already up-to-date
/// embeddings (content hash comparison against pgvector metadata) → generate
/// missing/stale embeddings (AI module) → upsert (pgvector) (docs task:
/// "SKU attribute embedding synchronization pipeline"). Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings.SynchronizeGoogleTaxonomyEmbeddingsHandler"/>.
///
/// Business orchestration only. Never references Azure, the OpenAI SDK,
/// Npgsql, Pgvector types or raw SQL directly.
///
/// Scope: only AttributeDefinition and AttributeOption are synchronized.
/// Catalog.SkuAttributeValues, MongoDB SKU documents and Google taxonomy
/// categories are never touched by this handler.
///
/// IsSearchable controls only catalog/storefront product search
/// participation; it does NOT control whether an attribute can be
/// semantically interpreted by AI. Every active Attribute Definition needs an
/// embedding so the Attribute Resolver can recognize fields supplied in
/// natural language, regardless of storefront searchability.
///
/// Skip decision (avoids unnecessary, costly provider calls): for a given
/// (EntityType, EntityId, Locale), if a persisted row already exists whose
/// stored ContentHash equals the freshly computed hash of the current
/// semantic text AND an embedding is already present, the item is skipped.
/// Otherwise the embedding is (re)generated. EmbeddedContentHash and
/// EmbeddedAt are only set after a successful embedding generation, so a
/// failed generation never marks a stale row as up-to-date.
/// </summary>
public sealed class SynchronizeAttributeEmbeddingsHandler
{
    private const string DefinitionEntityType = "AttributeDefinition";
    private const string OptionEntityType = "AttributeOption";
    private const string DefinitionSource = "YunuCommerce.Catalog.AttributeDefinitions";
    private const string OptionSource = "YunuCommerce.Catalog.AttributeOptions";

    private readonly IAttributeEmbeddingSourceRepository _sourceRepository;
    private readonly IAttributeEmbeddingRepository _embeddingRepository;
    private readonly EmbeddingOrchestrator _embeddingOrchestrator;
    private readonly IAttributeEmbeddingSynchronizationGuard _synchronizationGuard;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly AttributeEmbeddingsSyncOptions _syncOptions;
    private readonly ILogger<SynchronizeAttributeEmbeddingsHandler> _logger;

    public SynchronizeAttributeEmbeddingsHandler(
        IAttributeEmbeddingSourceRepository sourceRepository,
        IAttributeEmbeddingRepository embeddingRepository,
        EmbeddingOrchestrator embeddingOrchestrator,
        IAttributeEmbeddingSynchronizationGuard synchronizationGuard,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<AttributeEmbeddingsSyncOptions> syncOptions,
        ILogger<SynchronizeAttributeEmbeddingsHandler> logger)
    {
        _sourceRepository = sourceRepository;
        _embeddingRepository = embeddingRepository;
        _embeddingOrchestrator = embeddingOrchestrator;
        _synchronizationGuard = synchronizationGuard;
        _embeddingOptions = embeddingOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task<SynchronizeAttributeEmbeddingsResult> HandleAsync(
        SynchronizeAttributeEmbeddingsCommand command,
        CancellationToken cancellationToken)
    {
        using var lockToken = _synchronizationGuard.TryAcquire();

        if (lockToken is null)
        {
            throw new AttributeEmbeddingSynchronizationInProgressException();
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
        var existingMetadata = await _embeddingRepository.GetMetadataByLocaleAsync(locale, cancellationToken);
        var existingByKey = existingMetadata.ToDictionary(m => (m.EntityType, m.EntityId));

        _logger.LogInformation(
            "SKU attribute embedding synchronization started. Provider={Provider} DefinitionsRead={DefinitionsRead} OptionsRead={OptionsRead} BatchSize={BatchSize} MaxDegreeOfParallelism={MaxDegreeOfParallelism}",
            provider,
            definitions.Count,
            options.Count,
            batchSize,
            maxDegreeOfParallelism);

        var items = new List<PendingItem>(definitions.Count + options.Count);

        foreach (var definition in definitions)
        {
            var semanticText = AttributeSemanticDocumentBuilder.BuildDefinitionText(definition);
            var entityId = AttributeSemanticDocumentBuilder.BuildDefinitionEntityId(definition.Code);
            var metadata = JsonSerializer.Serialize(new
            {
                attributeDefinitionId = definition.AttributeDefinitionId,
                dataType = definition.DataType,
                cardinality = definition.Cardinality,
                unitFamily = definition.UnitFamily,
                isGoogleMerchantAttribute = definition.IsGoogleMerchantAttribute,
                isVariantAxis = definition.IsVariantAxis,
                isSearchable = definition.IsSearchable,
                isFilterable = definition.IsFilterable,
                isRequiredByDefault = definition.IsRequiredByDefault,
                source = DefinitionSource
            });

            items.Add(new PendingItem(
                DefinitionEntityType,
                entityId,
                definition.Code,
                OptionCode: null,
                Name: definition.Name,
                SemanticText: semanticText,
                Metadata: metadata,
                SourceUpdatedAt: definition.UpdatedAt));
        }

        foreach (var option in options)
        {
            var semanticText = AttributeSemanticDocumentBuilder.BuildOptionText(option);
            var entityId = AttributeSemanticDocumentBuilder.BuildOptionEntityId(option.AttributeCode, option.OptionCode);
            var metadata = JsonSerializer.Serialize(new
            {
                attributeOptionId = option.AttributeOptionId,
                attributeDefinitionId = option.AttributeDefinitionId,
                googleValue = option.GoogleValue,
                displayOrder = option.DisplayOrder,
                source = OptionSource
            });

            items.Add(new PendingItem(
                OptionEntityType,
                entityId,
                option.AttributeCode,
                OptionCode: option.OptionCode,
                Name: option.OptionName,
                SemanticText: semanticText,
                Metadata: metadata,
                SourceUpdatedAt: null));
        }

        var generated = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;
        string? resolvedModel = null;

        var batches = items
            .Select((item, index) => (item, index))
            .GroupBy(x => x.index / batchSize, x => x.item)
            .ToArray();

        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = batches[batchIndex].ToArray();

            _logger.LogInformation(
                "Processing SKU attribute embedding batch {BatchNumber}/{TotalBatches}",
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
                    var contentHash = AttributeSemanticDocumentBuilder.ComputeContentHash(item.SemanticText);

                    var hasExisting = existingByKey.TryGetValue((item.EntityType, item.EntityId), out var existing);

                    if (hasExisting && existing!.ContentHash == contentHash && existing.HasEmbedding)
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    try
                    {
                        var embeddingResult = await _embeddingOrchestrator.GenerateAsync(
                            item.SemanticText,
                            provider,
                            itemCancellationToken);

                        resolvedModel = embeddingResult.Model;

                        var now = DateTime.UtcNow;

                        var document = new AttributeEmbeddingDocument
                        {
                            Id = Guid.NewGuid(),
                            EntityType = item.EntityType,
                            EntityId = item.EntityId,
                            AttributeCode = item.AttributeCode,
                            OptionCode = item.OptionCode,
                            GoogleCategoryId = null,
                            SkuId = null,
                            Locale = locale,
                            Name = item.Name,
                            SemanticText = item.SemanticText,
                            Embedding = embeddingResult.Embedding,
                            EmbeddingModel = embeddingResult.Model,
                            ContentHash = contentHash,
                            EmbeddedContentHash = contentHash,
                            Metadata = item.Metadata,
                            SourceUpdatedAt = item.SourceUpdatedAt,
                            EmbeddedAt = now,
                            IsActive = true
                        };

                        await _embeddingRepository.UpsertAsync(document, itemCancellationToken);

                        if (hasExisting)
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
                            "Failed to generate/persist embedding for attribute entity {EntityType}/{EntityId}",
                            item.EntityType,
                            item.EntityId);
                    }
                });

            _logger.LogInformation(
                "Progress: Generated={Generated} Updated={Updated} Skipped={Skipped} Failed={Failed}",
                generated,
                updated,
                skipped,
                failed);
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "SKU attribute embedding synchronization completed. Provider={Provider} DefinitionsRead={DefinitionsRead} OptionsRead={OptionsRead} Generated={Generated} Updated={Updated} Skipped={Skipped} Failed={Failed} DurationMs={DurationMs}",
            provider,
            definitions.Count,
            options.Count,
            generated,
            updated,
            skipped,
            failed,
            stopwatch.ElapsedMilliseconds);

        return new SynchronizeAttributeEmbeddingsResult
        {
            Provider = provider,
            Model = resolvedModel ?? "n/a",
            DefinitionsRead = definitions.Count,
            OptionsRead = options.Count,
            Generated = generated,
            Updated = updated,
            Skipped = skipped,
            Deactivated = 0,
            Failed = failed,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }

    private sealed record PendingItem(
        string EntityType,
        string EntityId,
        string AttributeCode,
        string? OptionCode,
        string Name,
        string SemanticText,
        string Metadata,
        DateTime? SourceUpdatedAt);
}
