using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when a Product's lifecycle Status is changed via
/// <see cref="Product.TransitionTo"/> (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// </summary>
public sealed class ProductStatusChangedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public ProductStatus PreviousStatus { get; }

    public ProductStatus NewStatus { get; }

    public ProductStatusChangedDomainEvent(ProductId productId, ProductStatus previousStatus, ProductStatus newStatus)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
    }
}
