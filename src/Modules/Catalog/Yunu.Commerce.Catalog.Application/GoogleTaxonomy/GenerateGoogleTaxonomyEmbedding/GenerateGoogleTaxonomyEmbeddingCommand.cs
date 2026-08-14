namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

/// <summary>
/// Input for generating and persisting a semantic embedding of a Google
/// Product Taxonomy category hierarchy. <see cref="Provider"/> is optional;
/// when omitted, the AI module's configured DefaultProvider is used
/// (docs task: "GenerateGoogleTaxonomyEmbedding").
/// </summary>
public sealed record GenerateGoogleTaxonomyEmbeddingCommand(
    int GoogleCategoryId,
    string CategoryPath,
    string? Provider);
