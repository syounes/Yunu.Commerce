namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Port for persisting SKU attribute embeddings. Infrastructure implements
/// this against PostgreSQL + pgvector (public.sku_attribute_embeddings). The
/// Application layer never references Npgsql, Pgvector types or raw SQL
/// directly (docs task: "SKU attribute embedding synchronization pipeline").
/// </summary>
public interface IAttributeEmbeddingRepository
{
    /// <summary>
    /// Idempotently inserts or updates the row identified by
    /// (EntityType, EntityId, Locale). Preserves the existing PostgreSQL id
    /// during updates and returns the persisted row's Id.
    /// </summary>
    Task<Guid> UpsertAsync(AttributeEmbeddingDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns lightweight metadata (no vectors) for every embedding row
    /// currently persisted for the given locale, so callers can decide which
    /// entities are already synchronized without loading full vectors or
    /// calling the embedding provider unnecessarily.
    /// </summary>
    Task<IReadOnlyCollection<AttributeEmbeddingMetadata>> GetMetadataByLocaleAsync(
        string locale,
        CancellationToken cancellationToken = default);
}
