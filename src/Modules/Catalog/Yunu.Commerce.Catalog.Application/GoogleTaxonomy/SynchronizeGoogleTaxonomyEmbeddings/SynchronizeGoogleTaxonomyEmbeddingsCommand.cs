namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

/// <summary>
/// Input for synchronizing the pgvector projection of the entire active
/// Google Product Taxonomy. Both parameters are optional: when
/// <see cref="Provider"/> is omitted, the AI module's configured
/// DefaultProvider is used; when <see cref="BatchSize"/> is omitted, the
/// configured default batch size is used.
/// </summary>
public sealed record SynchronizeGoogleTaxonomyEmbeddingsCommand(
    string? Provider,
    int? BatchSize);
