namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Port for reading the SQL Server source data used by the SKU attribute
/// embedding synchronization pipeline (docs task: "SKU attribute embedding
/// synchronization pipeline"). Distinct from
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeCatalog.IAttributeCatalogRepository"/>,
/// which resolves single attributes/options for SKU creation; this port loads
/// the full active/searchable set used to build semantic documents.
/// </summary>
public interface IAttributeEmbeddingSourceRepository
{
    /// <summary>
    /// Returns active Attribute Definitions where IsSearchable = 1, ordered
    /// deterministically by Code.
    /// </summary>
    Task<IReadOnlyCollection<AttributeDefinitionSource>> GetActiveSearchableDefinitionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active Attribute Options whose owning Attribute Definition is
    /// also active, ordered deterministically by AttributeCode then OptionCode.
    /// </summary>
    Task<IReadOnlyCollection<AttributeOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default);
}
