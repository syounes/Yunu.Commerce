namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Lightweight description of a Segment embedding row that is pending
/// generation/regeneration, read from public.pending_segment_embeddings
/// without loading the stored vector (docs task: "Implementar sincronização de
/// embeddings de segmentos" - avoid unnecessary provider calls and avoid
/// loading full vectors just to decide what is pending).
/// </summary>
public sealed record SegmentEmbeddingPendingItem(
    Guid Id,
    string EntityType,
    long EntityId,
    long SegmentDefinitionId,
    long? SegmentOptionId,
    string SegmentCode,
    string? OptionCode,
    string Locale,
    string Name,
    string SemanticText,
    string ContentHash,
    string Metadata,
    DateTime? SourceUpdatedAt);
