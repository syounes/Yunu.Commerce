namespace Yunu.Commerce.Catalog.Application.AttributeCatalog;

/// <summary>
/// Dedicated read model for a Catalog Attribute Definition, decoupled from the
/// SQL Server row shape (Catalog.AttributeDefinitions,
/// deploy/sql/002_create_sku_attribute_catalog.sql). Contains only the fields
/// required to resolve and validate a SKU attribute assignment
/// (docs task: "SKU attribute foundation").
/// </summary>
public sealed class AttributeDefinitionResponse
{
    public required int AttributeDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string DataType { get; init; }

    public required string Cardinality { get; init; }

    public string? UnitFamily { get; init; }

    public string? ValidationRegex { get; init; }

    public decimal? MinNumericValue { get; init; }

    public decimal? MaxNumericValue { get; init; }

    public int? MaxLength { get; init; }

    public required bool IsVariantAxis { get; init; }

    public required bool IsSearchable { get; init; }

    public required bool IsFilterable { get; init; }

    public required bool IsActive { get; init; }
}
