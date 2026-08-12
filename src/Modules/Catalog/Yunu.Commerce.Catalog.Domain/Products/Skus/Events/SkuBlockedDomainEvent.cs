using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Skus.Events;

/// <summary>
/// Raised when a Sku transitions to Inactive ("blocked") (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// </summary>
public sealed class SkuBlockedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public ProductId ProductId { get; }

    public SkuBlockedDomainEvent(SkuId skuId, ProductId productId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        ProductId = productId;
    }
}
