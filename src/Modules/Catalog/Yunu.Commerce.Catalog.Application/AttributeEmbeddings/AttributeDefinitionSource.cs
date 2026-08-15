namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Read model for an active, searchable Catalog Attribute Definition used as
/// the source for semantic embedding generation (docs task: "SKU attribute
/// embedding synchronization pipeline"). Decoupled from the SQL Server row
/// shape (Catalog.AttributeDefinitions, deploy/sql/002_create_sku_attribute_catalog.sql).
/// </summary>
public sealed class AttributeDefinitionSource
{
    public required int AttributeDefinitionId { get; init; }

    public required string Code { get; init; }

    public string? GoogleAttributeName { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string SemanticText { get; init; }

    public required string DataType { get; init; }

    public required string Cardinality { get; init; }

    public string? UnitFamily { get; init; }

    public required bool IsGoogleMerchantAttribute { get; init; }

    public required bool IsVariantAxis { get; init; }

    public required bool IsSearchable { get; init; }

    public required bool IsFilterable { get; init; }

    public required bool IsRequiredByDefault { get; init; }

    public required int DisplayOrder { get; init; }

    public required bool IsActive { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
