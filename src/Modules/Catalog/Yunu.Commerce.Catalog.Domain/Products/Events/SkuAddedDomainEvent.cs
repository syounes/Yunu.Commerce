using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when a new Sku is added to a Product (docs/domains/catalog.md §38).
/// </summary>
public sealed class SkuAddedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public SkuId SkuId { get; }

    public SkuAddedDomainEvent(ProductId productId, SkuId skuId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
        SkuId = skuId;
    }
}
