namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Port for the PostgreSQL + pgvector Segment embedding projection
/// (public.segment_embeddings, deploy/databases/postgres/004_create_canonical_taxonomy_segment_vectors.sql,
/// deploy/databases/postgres/005-add-segment-assignment-scope.sql). The
/// Application layer never references Npgsql, Pgvector types or raw SQL
/// directly (docs task: "Implementar sincronização de embeddings de
/// segmentos").
///
/// The synchronization pipeline uses this port in two phases: first every
/// active source is upserted (creating new rows, refreshing content_hash and
/// reactivating rows that became active again, and invalidating the stale
/// vector by leaving embedded_content_hash out of sync), then only rows still
/// pending (missing embedding or stale hash/provider) are read and completed.
/// </summary>
public interface ISegmentEmbeddingRepository
{
    /// <summary>
    /// Returns the (EntityType, EntityId) keys currently persisted for the
    /// given locale, regardless of IsActive, so the handler can tell apart a
    /// brand-new projection ("Generated") from a refreshed one ("Updated")
    /// before calling <see cref="UpsertSourceAsync"/>.
    /// </summary>
    Task<IReadOnlyCollection<(string EntityType, long EntityId)>> GetExistingKeysAsync(
        string locale,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently inserts or updates the source projection row identified by
    /// (EntityType, EntityId, Locale) via public.upsert_segment_embedding_source.
    /// Preserves the existing PostgreSQL id and any previously generated
    /// vector; the row becomes ineligible for retrieval only if the freshly
    /// computed content_hash no longer matches embedded_content_hash.
    /// </summary>
    Task UpsertSourceAsync(SegmentEmbeddingSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks as inactive (IsActive = false) every currently active row for the
    /// given locale whose (EntityType, EntityId) is not present in
    /// <paramref name="activeKeys"/>. Never performs a physical DELETE. Returns
    /// the number of rows deactivated.
    /// </summary>
    Task<int> DeactivateMissingAsync(
        string locale,
        IReadOnlyCollection<(string EntityType, long EntityId)> activeKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active rows that still need embedding generation (missing
    /// embedding, stale content hash, or a different embedding provider),
    /// reading from public.pending_segment_embeddings. Never loads the stored
    /// vector.
    /// </summary>
    Task<IReadOnlyCollection<SegmentEmbeddingPendingItem>> GetPendingAsync(
        string locale,
        string provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimistically completes the embedding for one row via
    /// public.complete_segment_embedding. <paramref name="observedContentHash"/>
    /// must be the content_hash observed before the provider call started.
    /// Returns false when the source changed while the provider call was in
    /// flight (the source's content_hash no longer matches
    /// <paramref name="observedContentHash"/>); in that case the caller must
    /// not treat the item as completed and should leave it pending for the
    /// next run.
    /// </summary>
    Task<bool> CompleteAsync(
        string entityType,
        long entityId,
        string locale,
        string observedContentHash,
        string provider,
        string model,
        float[] embedding,
        CancellationToken cancellationToken = default);
}
