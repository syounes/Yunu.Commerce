namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

/// <summary>
/// Configuration for the Google Taxonomy embeddings batch synchronization
/// use case, bound from "Catalog:GoogleTaxonomyEmbeddings". These settings
/// govern processing of the taxonomy projection only; provider-specific
/// settings (endpoint, API key, deployment name) remain in the AI module
/// under "AI:Embeddings:Providers:{ProviderName}".
/// </summary>
public sealed class GoogleTaxonomyEmbeddingsSyncOptions
{
    public required int BatchSize { get; init; }

    public required int MaxDegreeOfParallelism { get; init; }
}
