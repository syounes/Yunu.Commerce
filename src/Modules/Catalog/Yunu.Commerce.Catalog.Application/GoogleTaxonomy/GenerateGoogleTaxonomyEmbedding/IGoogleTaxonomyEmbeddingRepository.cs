namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

/// <summary>
/// Port for persisting a Google Taxonomy category embedding. Infrastructure
/// implements this against PostgreSQL + pgvector. The Application layer never
/// references Npgsql, Pgvector types or raw SQL directly.
/// </summary>
public interface IGoogleTaxonomyEmbeddingRepository
{
    /// <summary>
    /// Idempotently inserts or updates the embedding identified by
    /// (GoogleCategoryId, Provider, Model). Returns the persisted row's Id.
    /// </summary>
    Task<Guid> UpsertAsync(GoogleTaxonomyEmbedding embedding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns lightweight metadata (no vectors) for every embedding currently
    /// persisted for the given provider, so callers can decide which
    /// categories are already synchronized without loading full vectors or
    /// calling the embedding provider unnecessarily.
    /// </summary>
    Task<IReadOnlyCollection<GoogleTaxonomyEmbeddingMetadata>> GetMetadataByProviderAsync(
        string provider,
        CancellationToken cancellationToken = default);
}
