namespace Yunu.Commerce.Catalog.Application.Skus.GetSkuById;

/// <summary>
/// Dedicated read model for a Sku, decoupled from the Domain Aggregate
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md). Includes
/// the Sku's assigned attributes (docs task: "SKU attribute foundation").
/// </summary>
public sealed class SkuDetailsResponse
{
    public required Guid SkuId { get; init; }

    public required Guid ProductId { get; init; }

    public required string Code { get; init; }

    public string? Gtin { get; init; }

    public required string Status { get; init; }

    public IReadOnlyCollection<SkuAttributeResponse> Attributes { get; init; } = Array.Empty<SkuAttributeResponse>();
}
