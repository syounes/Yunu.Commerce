namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// SQL Server read model for a Google Taxonomy category entry used by
/// resolution (GoogleTaxonomyCategories,
/// deploy/sql/001-google-taxonomy-tables.sql). Distinct from <see
/// cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GoogleTaxonomyCategoryResponse"/>,
/// which is the public query read model; this entry is scoped internally to
/// category hint resolution.
/// </summary>
public sealed record GoogleCategoryCatalogEntry(
    long GoogleCategoryId,
    string Name,
    string FullPath,
    int Level,
    bool IsLeaf,
    bool IsActive);

/// <summary>
/// Batch-oriented SQL Server read port used by Google category hint
/// resolution (docs task: "Google Category Resolution"). Distinct from <see
/// cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.IGoogleTaxonomyRepository"/>,
/// which serves the public taxonomy query/browse endpoints and the
/// synchronization pipeline; this port batches lookups the same way <see
/// cref="Yunu.Commerce.Catalog.Application.AttributeResolution.IAttributeCatalogReader"/>
/// does for attributes, to avoid one SQL Server round-trip per candidate.
/// </summary>
public interface IGoogleCategoryCatalogReader
{
    /// <summary>
    /// Finds active categories whose Id, Name or FullPath exactly matches
    /// (case/accent-insensitive, trimmed) the given raw hint, in a single
    /// query. Multiple matches (e.g. an ambiguous Name shared by categories
    /// in different branches) must all be returned so the caller can decide
    /// whether to treat the hint as ambiguous.
    /// </summary>
    Task<IReadOnlyList<GoogleCategoryCatalogEntry>> FindExactMatchesAsync(
        string categoryHint,
        string locale,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hydrates active categories by id, in a single batched query,
    /// preserving no particular order (callers must re-key by id).
    /// </summary>
    Task<IReadOnlyList<GoogleCategoryCatalogEntry>> GetByIdsAsync(
        IReadOnlyCollection<long> googleCategoryIds,
        CancellationToken cancellationToken);
}
