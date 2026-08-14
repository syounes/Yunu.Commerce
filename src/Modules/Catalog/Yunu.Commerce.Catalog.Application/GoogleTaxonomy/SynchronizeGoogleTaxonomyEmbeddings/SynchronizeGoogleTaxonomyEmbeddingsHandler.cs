using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yunu.Commerce.AI.Application.Embeddings;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

/// <summary>
/// Orchestrates a full Google Product Taxonomy embedding synchronization:
/// read active categories (SQL Server) → skip already up-to-date embeddings
/// (pgvector metadata) → generate missing/changed embeddings in limited-concurrency
/// batches (AI module) → upsert (pgvector) (docs task:
/// "SynchronizeGoogleTaxonomyEmbeddings"). This is a projection sync distinct
/// from <see cref="SynchronizeGoogleTaxonomy.SynchronizeGoogleTaxonomyHandler"/>,
/// which imports the official Google feed into SQL Server.
///
/// Business orchestration only. Never references Azure, the OpenAI SDK, Npgsql
/// or Pgvector types directly.
///
/// Skip decision (avoids unnecessary, costly provider calls): for a given
/// (GoogleCategoryId, Provider), if a persisted embedding already exists and
/// its stored CategoryPath equals the category's current FullPath, the
/// category is skipped. Otherwise the embedding is (re)generated. In this
/// phase a provider is assumed to map to a single active model, so metadata
/// is looked up by provider only.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyEmbeddingsHandler
{
    private readonly IGoogleTaxonomyRepository _taxonomyRepository;
    private readonly IGoogleTaxonomyEmbeddingRepository _embeddingRepository;
    private readonly EmbeddingOrchestrator _embeddingOrchestrator;
    private readonly IGoogleTaxonomyEmbeddingSynchronizationGuard _synchronizationGuard;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly GoogleTaxonomyEmbeddingsSyncOptions _syncOptions;
    private readonly ILogger<SynchronizeGoogleTaxonomyEmbeddingsHandler> _logger;

    public SynchronizeGoogleTaxonomyEmbeddingsHandler(
        IGoogleTaxonomyRepository taxonomyRepository,
        IGoogleTaxonomyEmbeddingRepository embeddingRepository,
        EmbeddingOrchestrator embeddingOrchestrator,
        IGoogleTaxonomyEmbeddingSynchronizationGuard synchronizationGuard,
        IOptions<EmbeddingOptions> embeddingOptions,
        IOptions<GoogleTaxonomyEmbeddingsSyncOptions> syncOptions,
        ILogger<SynchronizeGoogleTaxonomyEmbeddingsHandler> logger)
    {
        _taxonomyRepository = taxonomyRepository;
        _embeddingRepository = embeddingRepository;
        _embeddingOrchestrator = embeddingOrchestrator;
        _synchronizationGuard = synchronizationGuard;
        _embeddingOptions = embeddingOptions.Value;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task<SynchronizeGoogleTaxonomyEmbeddingsResult> HandleAsync(
        SynchronizeGoogleTaxonomyEmbeddingsCommand command,
        CancellationToken cancellationToken)
    {
        using var lockToken = _synchronizationGuard.TryAcquire();

        if (lockToken is null)
        {
            throw new GoogleTaxonomyEmbeddingSynchronizationInProgressException();
        }

        var provider = string.IsNullOrWhiteSpace(command.Provider)
            ? _embeddingOptions.DefaultProvider
            : command.Provider;

        var batchSize = command.BatchSize is > 0 ? command.BatchSize.Value : _syncOptions.BatchSize;
        var maxDegreeOfParallelism = _syncOptions.MaxDegreeOfParallelism;

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var activeCategories = await _taxonomyRepository.GetActiveAsync(cancellationToken);
        var existingMetadata = await _embeddingRepository.GetMetadataByProviderAsync(provider, cancellationToken);
        var existingByCategoryId = existingMetadata.ToDictionary(m => m.GoogleCategoryId);

        _logger.LogInformation(
            "Google taxonomy embedding synchronization started. Provider={Provider} TotalActiveCategories={TotalCategories} BatchSize={BatchSize} MaxDegreeOfParallelism={MaxDegreeOfParallelism}",
            provider,
            activeCategories.Count,
            batchSize,
            maxDegreeOfParallelism);

        var processed = 0;
        var generated = 0;
        var skipped = 0;
        var failed = 0;
        string? resolvedModel = existingMetadata.Select(m => m.Model).FirstOrDefault();

        var batches = activeCategories
            .Select((category, index) => (category, index))
            .GroupBy(x => x.index / batchSize, x => x.category)
            .ToArray();

        for (var batchIndex = 0; batchIndex < batches.Length; batchIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = batches[batchIndex].ToArray();

            _logger.LogInformation(
                "Processing Google taxonomy embedding batch {BatchNumber}/{TotalBatches}",
                batchIndex + 1,
                batches.Length);

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                },
                async (category, itemCancellationToken) =>
                {
                    Interlocked.Increment(ref processed);

                    if (existingByCategoryId.TryGetValue(category.GoogleCategoryId, out var existing) &&
                        existing.CategoryPath == category.FullPath)
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    try
                    {
                        var embeddingResult = await _embeddingOrchestrator.GenerateAsync(
                            category.FullPath,
                            provider,
                            itemCancellationToken);

                        resolvedModel = embeddingResult.Model;

                        var now = DateTime.UtcNow;

                        var embedding = new GoogleTaxonomyEmbedding
                        {
                            Id = Guid.NewGuid(),
                            GoogleCategoryId = category.GoogleCategoryId,
                            CategoryPath = category.FullPath,
                            Provider = embeddingResult.Provider,
                            Model = embeddingResult.Model,
                            Dimensions = embeddingResult.Dimensions,
                            Embedding = embeddingResult.Embedding,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        };

                        await _embeddingRepository.UpsertAsync(embedding, itemCancellationToken);

                        Interlocked.Increment(ref generated);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Interlocked.Increment(ref failed);

                        _logger.LogError(
                            ex,
                            "Failed to generate/persist embedding for Google category {GoogleCategoryId}",
                            category.GoogleCategoryId);
                    }
                });

            _logger.LogInformation(
                "Progress: Processed={Processed} Generated={Generated} Skipped={Skipped} Failed={Failed}",
                processed,
                generated,
                skipped,
                failed);
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Google taxonomy embedding synchronization completed. Provider={Provider} Processed={Processed} Generated={Generated} Skipped={Skipped} Failed={Failed} DurationMs={DurationMs}",
            provider,
            processed,
            generated,
            skipped,
            failed,
            stopwatch.ElapsedMilliseconds);

        return new SynchronizeGoogleTaxonomyEmbeddingsResult
        {
            Provider = provider,
            Model = resolvedModel ?? "n/a",
            TotalCategories = activeCategories.Count,
            Processed = processed,
            Generated = generated,
            Skipped = skipped,
            Failed = failed,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
}
