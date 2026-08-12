using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when a new Product is created (docs/domains/catalog.md §38).
/// </summary>
public sealed class ProductCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public ProductCreatedDomainEvent(ProductId productId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
    }
}
