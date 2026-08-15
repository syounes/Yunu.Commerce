namespace Yunu.Commerce.Api.AttributeEmbeddings;

/// <summary>
/// HTTP request to synchronize the pgvector projection of the active SKU
/// attribute catalog (AttributeDefinitions + AttributeOptions). Both fields
/// are optional. Mirrors
/// <see cref="Yunu.Commerce.Api.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddingsRequest"/>.
/// </summary>
public sealed class SynchronizeAttributeEmbeddingsRequest
{
    public string? Provider { get; init; }

    public int? BatchSize { get; init; }
}
