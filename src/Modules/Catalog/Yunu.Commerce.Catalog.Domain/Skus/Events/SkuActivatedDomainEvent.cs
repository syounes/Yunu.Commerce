using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Skus.Events;

/// <summary>
/// Raised when a Sku transitions to Active (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// </summary>
public sealed class SkuActivatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public ProductId ProductId { get; }

    public SkuActivatedDomainEvent(SkuId skuId, ProductId productId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        ProductId = productId;
    }
}
