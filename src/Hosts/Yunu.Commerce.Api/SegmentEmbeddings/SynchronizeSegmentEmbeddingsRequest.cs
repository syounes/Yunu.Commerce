namespace Yunu.Commerce.Api.SegmentEmbeddings;

/// <summary>
/// HTTP request to synchronize the pgvector projection of the active Segment
/// catalog (SegmentDefinitions + SegmentOptions). Both fields are optional.
/// Mirrors
/// <see cref="Yunu.Commerce.Api.AttributeEmbeddings.SynchronizeAttributeEmbeddingsRequest"/>.
/// </summary>
public sealed class SynchronizeSegmentEmbeddingsRequest
{
    public string? Provider { get; init; }

    public int? BatchSize { get; init; }
}
