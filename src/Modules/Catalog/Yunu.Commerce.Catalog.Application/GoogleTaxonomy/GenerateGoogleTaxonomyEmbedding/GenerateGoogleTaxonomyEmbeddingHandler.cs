using Microsoft.Extensions.Logging;
using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

/// <summary>
/// Orchestrates generation and persistence of a Google Taxonomy category
/// embedding: request embedding → build persistence model → upsert
/// (docs task: "GenerateGoogleTaxonomyEmbedding"). Business orchestration
/// only; embedding generation is delegated to <see cref="EmbeddingOrchestrator"/>
/// (AI module) and persistence to <see cref="IGoogleTaxonomyEmbeddingRepository"/>.
/// This handler never references Azure, Npgsql or any vendor-specific type.
/// </summary>
public sealed class GenerateGoogleTaxonomyEmbeddingHandler
{
    private readonly EmbeddingOrchestrator _embeddingOrchestrator;
    private readonly IGoogleTaxonomyEmbeddingRepository _embeddingRepository;
    private readonly ILogger<GenerateGoogleTaxonomyEmbeddingHandler> _logger;

    public GenerateGoogleTaxonomyEmbeddingHandler(
        EmbeddingOrchestrator embeddingOrchestrator,
        IGoogleTaxonomyEmbeddingRepository embeddingRepository,
        ILogger<GenerateGoogleTaxonomyEmbeddingHandler> logger)
    {
        _embeddingOrchestrator = embeddingOrchestrator;
        _embeddingRepository = embeddingRepository;
        _logger = logger;
    }

    public async Task<GenerateGoogleTaxonomyEmbeddingResult> HandleAsync(
        GenerateGoogleTaxonomyEmbeddingCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating Google taxonomy embedding for category {GoogleCategoryId} using provider {Provider}",
            command.GoogleCategoryId,
            command.Provider ?? "(default)");

        var embeddingResult = await _embeddingOrchestrator.GenerateAsync(
            command.CategoryPath,
            command.Provider,
            cancellationToken);

        _logger.LogInformation(
            "Embedding generated for category {GoogleCategoryId} with {Dimensions} dimensions",
            command.GoogleCategoryId,
            embeddingResult.Dimensions);

        var now = DateTime.UtcNow;

        var embedding = new GoogleTaxonomyEmbedding
        {
            Id = Guid.NewGuid(),
            GoogleCategoryId = command.GoogleCategoryId,
            CategoryPath = command.CategoryPath,
            Provider = embeddingResult.Provider,
            Model = embeddingResult.Model,
            Dimensions = embeddingResult.Dimensions,
            Embedding = embeddingResult.Embedding,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var persistedId = await _embeddingRepository.UpsertAsync(embedding, cancellationToken);

        _logger.LogInformation(
            "Embedding persisted for category {GoogleCategoryId} with id {EmbeddingId}",
            command.GoogleCategoryId,
            persistedId);

        return new GenerateGoogleTaxonomyEmbeddingResult
        {
            Id = persistedId,
            GoogleCategoryId = command.GoogleCategoryId,
            CategoryPath = command.CategoryPath,
            Provider = embeddingResult.Provider,
            Model = embeddingResult.Model,
            Dimensions = embeddingResult.Dimensions
        };
    }
}
