namespace Yunu.Commerce.Catalog.Application.Skus;

/// <summary>
/// Dedicated read model for one Sku attribute assignment, decoupled from the
/// Domain Aggregate and shared by every Sku read model that exposes
/// attributes (docs task: "SKU attribute foundation").
/// </summary>
public sealed class SkuAttributeResponse
{
    public required int AttributeDefinitionId { get; init; }

    public required string AttributeCode { get; init; }

    public required int Sequence { get; init; }

    public required string DataType { get; init; }

    public string? RawValue { get; init; }

    public required string NormalizedValue { get; init; }

    public int? AttributeOptionId { get; init; }

    public required string Source { get; init; }

    public decimal? Confidence { get; init; }
}
