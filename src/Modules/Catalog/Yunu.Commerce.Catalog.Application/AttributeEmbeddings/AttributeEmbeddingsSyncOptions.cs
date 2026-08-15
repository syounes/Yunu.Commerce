namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Configuration for the SKU attribute embeddings batch synchronization use
/// case, bound from "Catalog:AttributeEmbeddings". Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings.GoogleTaxonomyEmbeddingsSyncOptions"/>.
/// Provider-specific settings (endpoint, API key, deployment name) remain in
/// the AI module under "AI:Embeddings:Providers:{ProviderName}".
/// </summary>
public sealed class AttributeEmbeddingsSyncOptions
{
    public required int BatchSize { get; init; }

    public required int MaxDegreeOfParallelism { get; init; }

    public required string Locale { get; init; }
}
