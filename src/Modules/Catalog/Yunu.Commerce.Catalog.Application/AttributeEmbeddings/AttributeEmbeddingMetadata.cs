namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Lightweight metadata about a persisted attribute embedding row, used to
/// decide whether a (re)generation is needed without loading the full vector
/// (docs task: "SKU attribute embedding synchronization pipeline" - avoid
/// unnecessary provider calls). Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding.GoogleTaxonomyEmbeddingMetadata"/>.
/// </summary>
public sealed record AttributeEmbeddingMetadata(
    string EntityType,
    string EntityId,
    string Locale,
    string ContentHash,
    string? EmbeddedContentHash,
    bool HasEmbedding);
