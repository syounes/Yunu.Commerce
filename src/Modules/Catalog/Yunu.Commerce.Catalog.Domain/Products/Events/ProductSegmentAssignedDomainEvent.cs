using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when a new Segment is assigned to a Product for the first time
/// (docs task: "Canonical Taxonomy + Segments Domain" §39). Not raised for
/// an idempotent re-assignment of the same effective options.
/// </summary>
public sealed class ProductSegmentAssignedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public string SegmentCode { get; }

    public ProductSegmentAssignedDomainEvent(ProductId productId, SegmentDefinitionId segmentDefinitionId, string segmentCode)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
        SegmentDefinitionId = segmentDefinitionId;
        SegmentCode = segmentCode;
    }
}
