namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Port for reading the SQL Server source data used by the SKU attribute
/// embedding synchronization pipeline (docs task: "SKU attribute embedding
/// synchronization pipeline"). Distinct from
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeCatalog.IAttributeCatalogRepository"/>,
/// which resolves single attributes/options for SKU creation; this port loads
/// the full active set used to build semantic documents.
///
/// <see cref="AttributeDefinitionSource.IsSearchable"/> controls whether an
/// attribute participates in catalog/storefront product search. It does NOT
/// control whether the attribute can be semantically interpreted by AI.
/// Every active Attribute Definition needs an embedding so the Attribute
/// Resolver can recognize fields supplied in natural language, regardless of
/// its storefront searchability.
/// </summary>
public interface IAttributeEmbeddingSourceRepository
{
    /// <summary>
    /// Returns every active Attribute Definition (IsActive = 1), regardless of
    /// IsSearchable, ordered deterministically by Code.
    /// </summary>
    Task<IReadOnlyCollection<AttributeDefinitionSource>> GetActiveDefinitionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active Attribute Options whose owning Attribute Definition is
    /// also active, ordered deterministically by AttributeCode then OptionCode.
    /// </summary>
    Task<IReadOnlyCollection<AttributeOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default);
}
