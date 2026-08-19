namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Application-level input for upserting one row of the PostgreSQL
/// public.segment_embeddings source projection (docs task: "Implementar
/// sincronização de embeddings de segmentos"). Maps 1:1 to the parameters of
/// public.upsert_segment_embedding_source
/// (deploy/databases/postgres/005-add-segment-assignment-scope.sql). This is
/// a projection/synchronization artifact, not a Catalog Domain concept.
/// </summary>
public sealed class SegmentEmbeddingSource
{
    public required string EntityType { get; init; }

    public required long EntityId { get; init; }

    public required long SegmentDefinitionId { get; init; }

    public long? SegmentOptionId { get; init; }

    public required string SegmentCode { get; init; }

    public string? OptionCode { get; init; }

    public required string AssignmentScope { get; init; }

    public required string Locale { get; init; }

    public required string Name { get; init; }

    public required string SemanticText { get; init; }

    public required string Metadata { get; init; }

    public DateTime? SourceUpdatedAt { get; init; }
}
