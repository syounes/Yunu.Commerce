using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Skus.Events;

/// <summary>
/// Raised when a Segment assignment is removed from a Sku (docs task:
/// "Canonical Taxonomy + Segments Domain" §39).
/// </summary>
public sealed class SkuSegmentRemovedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public SkuSegmentRemovedDomainEvent(SkuId skuId, SegmentDefinitionId segmentDefinitionId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        SegmentDefinitionId = segmentDefinitionId;
    }
}
