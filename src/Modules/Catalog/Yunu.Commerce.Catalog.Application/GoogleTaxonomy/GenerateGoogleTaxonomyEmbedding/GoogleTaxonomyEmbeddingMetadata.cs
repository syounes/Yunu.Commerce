namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

/// <summary>
/// Lightweight metadata about a persisted Google Taxonomy category embedding,
/// used to decide whether a (re)generation is needed without loading the full
/// vector (docs task: "SynchronizeGoogleTaxonomyEmbeddings" - avoid unnecessary
/// provider calls).
/// </summary>
public sealed record GoogleTaxonomyEmbeddingMetadata(
    int GoogleCategoryId,
    string Provider,
    string Model,
    string CategoryPath);
