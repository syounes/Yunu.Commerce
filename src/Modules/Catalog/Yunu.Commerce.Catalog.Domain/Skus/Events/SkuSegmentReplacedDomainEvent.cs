using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Skus.Events;

/// <summary>
/// Raised when an existing Sku Segment assignment's options are explicitly
/// replaced (docs task: "Canonical Taxonomy + Segments Domain" §39).
/// </summary>
public sealed class SkuSegmentReplacedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public string SegmentCode { get; }

    public SkuSegmentReplacedDomainEvent(SkuId skuId, SegmentDefinitionId segmentDefinitionId, string segmentCode)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        SegmentDefinitionId = segmentDefinitionId;
        SegmentCode = segmentCode;
    }
}
