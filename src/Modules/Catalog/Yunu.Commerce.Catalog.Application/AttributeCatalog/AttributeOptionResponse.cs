namespace Yunu.Commerce.Catalog.Application.AttributeCatalog;

/// <summary>
/// Dedicated read model for a Catalog Attribute Option, decoupled from the SQL
/// Server row shape (Catalog.AttributeOptions,
/// deploy/databases/sqlserver/002_create_sku_attribute_catalog.sql). Only used when the owning
/// Attribute Definition's DataType is Enum (docs task: "SKU attribute
/// foundation").
/// </summary>
public sealed class AttributeOptionResponse
{
    public required int AttributeOptionId { get; init; }

    public required int AttributeDefinitionId { get; init; }

    public required string Code { get; init; }

    public string? GoogleValue { get; init; }

    public required string Name { get; init; }

    public required bool IsActive { get; init; }
}
