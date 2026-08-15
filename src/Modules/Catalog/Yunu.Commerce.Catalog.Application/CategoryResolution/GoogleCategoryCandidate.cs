namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// A candidate Google Product Taxonomy category considered while resolving a
/// category hint (docs task: "Google Category Resolution"), already hydrated
/// and validated against SQL Server (GoogleTaxonomyCategories, the source of
/// truth). pgvector similarity alone is never sufficient to appear here.
/// </summary>
public sealed record GoogleCategoryCandidate(
    long GoogleCategoryId,
    string CategoryName,
    string CategoryPath,
    int? Depth,
    double Similarity);
