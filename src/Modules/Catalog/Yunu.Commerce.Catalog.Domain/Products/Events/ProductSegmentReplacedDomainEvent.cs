using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Products.Events;

/// <summary>
/// Raised when an existing Product Segment assignment's options are
/// explicitly replaced (docs task: "Canonical Taxonomy + Segments Domain" §39).
/// </summary>
public sealed class ProductSegmentReplacedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductId ProductId { get; }

    public SegmentDefinitionId SegmentDefinitionId { get; }

    public string SegmentCode { get; }

    public ProductSegmentReplacedDomainEvent(ProductId productId, SegmentDefinitionId segmentDefinitionId, string segmentCode)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductId = productId;
        SegmentDefinitionId = segmentDefinitionId;
        SegmentCode = segmentCode;
    }
}
