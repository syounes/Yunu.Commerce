using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Skus.Events;

/// <summary>
/// Raised when a new Segment is assigned to a Sku for the first time
/// (docs task: "Canonical Taxonomy + Segments Domain" §39). Not raised for
/// an idempotent re-assignment of the same effective options.
/// </summary>
public sealed class SkuSegmentAssignedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public string SegmentCode { get; }

    public SkuSegmentAssignedDomainEvent(SkuId skuId, SegmentDefinitionId segmentDefinitionId, string segmentCode)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        SegmentDefinitionId = segmentDefinitionId;
        SegmentCode = segmentCode;
    }
}
