namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Read model for an active Catalog Attribute Option whose owning Attribute
/// Definition is also active, used as the source for semantic embedding
/// generation (docs task: "SKU attribute embedding synchronization pipeline").
/// Decoupled from the SQL Server row shape (Catalog.AttributeOptions,
/// deploy/sql/002_create_sku_attribute_catalog.sql).
/// </summary>
public sealed class AttributeOptionSource
{
    public required int AttributeOptionId { get; init; }

    public required int AttributeDefinitionId { get; init; }

    public required string AttributeCode { get; init; }

    public required string AttributeName { get; init; }

    public required string OptionCode { get; init; }

    public string? GoogleValue { get; init; }

    public required string OptionName { get; init; }

    public required string OptionSemanticText { get; init; }

    public required int DisplayOrder { get; init; }

    public required bool IsActive { get; init; }
}
