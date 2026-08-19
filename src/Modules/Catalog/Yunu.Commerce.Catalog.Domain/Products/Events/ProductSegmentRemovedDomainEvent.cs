using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when a Segment assignment is removed from a Product (docs task:
/// "Canonical Taxonomy + Segments Domain" §39).
/// </summary>
public sealed class ProductSegmentRemovedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public ProductSegmentRemovedDomainEvent(ProductId productId, SegmentDefinitionId segmentDefinitionId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
        SegmentDefinitionId = segmentDefinitionId;
    }
}
