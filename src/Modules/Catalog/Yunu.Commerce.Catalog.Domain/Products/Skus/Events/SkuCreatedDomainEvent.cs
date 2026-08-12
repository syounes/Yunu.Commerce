using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Skus.Events;

/// <summary>
/// Raised when a new Sku Aggregate is created (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// </summary>
public sealed class SkuCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public ProductId ProductId { get; }

    public SkuCreatedDomainEvent(SkuId skuId, ProductId productId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        ProductId = productId;
    }
}
