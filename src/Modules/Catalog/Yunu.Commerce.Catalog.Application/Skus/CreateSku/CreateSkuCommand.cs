namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Input for creating a new Sku (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// SkuId is intentionally absent: identity is assigned internally by
/// <see cref="CreateSkuHandler"/> via SkuId.New().
/// </summary>
public sealed class CreateSkuCommand
{
    public required Guid ProductId { get; init; }

    public required string Code { get; init; }

    public string? Gtin { get; init; }
}
