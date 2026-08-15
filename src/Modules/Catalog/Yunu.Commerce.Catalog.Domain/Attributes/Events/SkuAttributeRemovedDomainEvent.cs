using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Attributes.Events;

/// <summary>
/// Raised when a Sku attribute assignment is removed
/// (docs task: "SKU attribute foundation").
/// </summary>
public sealed class SkuAttributeRemovedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public AttributeDefinitionId AttributeDefinitionId { get; }

    public int Sequence { get; }

    public SkuAttributeRemovedDomainEvent(SkuId skuId, AttributeDefinitionId attributeDefinitionId, int sequence)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        AttributeDefinitionId = attributeDefinitionId;
        Sequence = sequence;
    }
}
