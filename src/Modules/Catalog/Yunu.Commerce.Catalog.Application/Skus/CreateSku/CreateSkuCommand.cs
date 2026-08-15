namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Input for creating a new Sku (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// SkuId is intentionally absent: identity is assigned internally by
/// <see cref="CreateSkuHandler"/> via SkuId.New().
///
/// Attributes are optional and explicit only (docs task: "SKU attribute
/// foundation"): each entry supplies an attribute Code plus either a raw
/// Value or an OptionCode, resolved and validated against SQL Server by
/// <see cref="CreateSkuHandler"/> before the Sku Aggregate assigns them.
/// </summary>
public sealed class CreateSkuCommand
{
    public required Guid ProductId { get; init; }

    public required string Code { get; init; }

    public string? Gtin { get; init; }

    public IReadOnlyCollection<SkuAttributeInput> Attributes { get; init; } = Array.Empty<SkuAttributeInput>();
}
