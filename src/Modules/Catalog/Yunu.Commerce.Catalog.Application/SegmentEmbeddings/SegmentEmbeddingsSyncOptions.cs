namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Configuration for the Segment embeddings batch synchronization use case,
/// bound from "Catalog:SegmentEmbeddings". Provider-specific settings
/// (endpoint, API key, deployment name) remain in the AI module under
/// "AI:Embeddings:Providers:{ProviderName}".
/// </summary>
public sealed class SegmentEmbeddingsSyncOptions
{
    public required int BatchSize { get; init; }

    public required int MaxDegreeOfParallelism { get; init; }

    public required string Locale { get; init; }
}
