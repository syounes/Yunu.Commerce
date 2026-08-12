using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when an existing Product's name is changed to a different value
/// (docs/domains/catalog.md §38). Not raised when the new name equals the current name.
/// </summary>
public sealed class ProductRenamedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public ProductName PreviousName { get; }

    public ProductName NewName { get; }

    public ProductRenamedDomainEvent(ProductId productId, ProductName previousName, ProductName newName)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
        PreviousName = previousName;
        NewName = newName;
    }
}
