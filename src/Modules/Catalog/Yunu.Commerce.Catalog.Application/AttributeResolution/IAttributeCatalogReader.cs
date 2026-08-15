namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// SQL Server read model for an Attribute Definition used by resolution
/// (Catalog.AttributeDefinitions, deploy/sql/002_create_sku_attribute_catalog.sql).
/// </summary>
public sealed record AttributeDefinitionCatalogEntry(
    int AttributeDefinitionId,
    string Code,
    string Name,
    string? GoogleAttributeName,
    string DataType,
    string Cardinality,
    string? UnitFamily,
    string? ValidationRegex,
    decimal? MinNumericValue,
    decimal? MaxNumericValue,
    int? MaxLength,
    bool IsActive);

/// <summary>
/// SQL Server read model for an Attribute Option used by resolution
/// (Catalog.AttributeOptions, deploy/sql/002_create_sku_attribute_catalog.sql).
/// </summary>
public sealed record AttributeOptionCatalogEntry(
    int AttributeOptionId,
    int AttributeDefinitionId,
    string Code,
    string? GoogleValue,
    string Name,
    bool IsActive);

/// <summary>
/// SQL Server read model for a Google category attribute rule
/// (Catalog.GoogleCategoryAttributeRules,
/// deploy/sql/002_create_sku_attribute_catalog.sql).
/// </summary>
public sealed record GoogleCategoryAttributeRuleEntry(
    long GoogleCategoryId,
    int AttributeDefinitionId,
    string RequirementLevel,
    bool IsVariantAxis);

/// <summary>
/// Batch-oriented SQL Server read port used by attribute hint resolution
/// (docs task: "Semantic attribute hint resolution"). Distinct from
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeCatalog.IAttributeCatalogRepository"/>,
/// which resolves one attribute/option at a time for the CreateSku use case;
/// resolving a batch of hints needs to hydrate/validate many exact-match and
/// pgvector candidates without issuing one query per candidate.
/// </summary>
public interface IAttributeCatalogReader
{
    /// <summary>
    /// Finds active Attribute Definitions whose Code, Name or
    /// GoogleAttributeName exactly matches (case/accent-insensitive) any of
    /// the given normalized values, in a single batched query.
    /// </summary>
    Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> FindDefinitionsByExactMatchAsync(
        IReadOnlyCollection<string> normalizedValues,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hydrates active Attribute Definitions by Code, in a single batched
    /// query, preserving no particular order (callers must re-key by Code).
    /// </summary>
    Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> GetDefinitionsByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds active Attribute Options owned by <paramref name="attributeDefinitionId"/>
    /// whose Code, Name or GoogleValue exactly matches (case/accent-insensitive)
    /// any of the given normalized values.
    /// </summary>
    Task<IReadOnlyList<AttributeOptionCatalogEntry>> FindOptionsByExactMatchAsync(
        int attributeDefinitionId,
        IReadOnlyCollection<string> normalizedValues,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hydrates active Attribute Options owned by
    /// <paramref name="attributeDefinitionId"/> by Code, in a single batched
    /// query.
    /// </summary>
    Task<IReadOnlyList<AttributeOptionCatalogEntry>> GetOptionsByCodesAsync(
        int attributeDefinitionId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the Google category attribute rules applicable to
    /// <paramref name="googleCategoryId"/> for the given set of Attribute
    /// Definition ids (Catalog.GoogleCategoryAttributeRules).
    /// </summary>
    Task<IReadOnlyList<GoogleCategoryAttributeRuleEntry>> GetCategoryRulesAsync(
        long googleCategoryId,
        IReadOnlyCollection<int> attributeDefinitionIds,
        CancellationToken cancellationToken);
}
