namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// HTTP request to synchronize the pgvector projection of the entire active
/// Google Product Taxonomy. Both fields are optional and the body may be
/// omitted entirely (equivalent to <c>{}</c>). When <see cref="Provider"/> is
/// omitted, the AI module's configured DefaultProvider is used; when
/// <see cref="BatchSize"/> is omitted, the configured default batch size is used.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyEmbeddingsRequest
{
    public string? Provider { get; init; }

    public int? BatchSize { get; init; }
}
