namespace Yunu.Commerce.Catalog.Application.AttributeCatalog;

/// <summary>
/// Port for resolving SKU attribute reference data from SQL Server
/// (Catalog.AttributeDefinitions, Catalog.AttributeOptions -
/// deploy/databases/sqlserver/002_create_sku_attribute_catalog.sql). Catalog.Domain never
/// accesses SQL Server directly; the Application layer resolves and validates
/// definitions/options through this port before asking the Sku Aggregate to
/// assign an attribute (docs task: "SKU attribute foundation").
///
/// GoogleCategoryAttributeRules retrieval is deferred until a use case
/// requires category-specific requirement levels; only definition/option
/// lookup needed by the current CreateSku use case is exposed here
/// (docs §49, "avoid speculative abstractions").
/// </summary>
public interface IAttributeCatalogRepository
{
    Task<AttributeDefinitionResponse?> GetDefinitionByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<AttributeDefinitionResponse?> GetDefinitionByIdAsync(
        int attributeDefinitionId,
        CancellationToken cancellationToken);

    Task<AttributeOptionResponse?> GetOptionAsync(
        int attributeDefinitionId,
        string optionCode,
        CancellationToken cancellationToken);
}
