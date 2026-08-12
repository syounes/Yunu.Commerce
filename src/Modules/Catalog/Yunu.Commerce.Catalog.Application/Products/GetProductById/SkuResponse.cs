namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Dedicated read model for a Sku, decoupled from the Domain Entity
/// (docs/domains/catalog.md §51).
/// </summary>
public sealed class SkuResponse
{
    public required Guid SkuId { get; init; }

    public required string Code { get; init; }

    public required string Status { get; init; }
}
