using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Attributes.Events;

/// <summary>
/// Raised when a new attribute is assigned to a Sku for the first time
/// (docs task: "SKU attribute foundation"). Not raised for an idempotent
/// re-assignment of the same effective value.
/// </summary>
public sealed class SkuAttributeAssignedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public AttributeDefinitionId AttributeDefinitionId { get; }

    public string AttributeCode { get; }

    public int Sequence { get; }

    public SkuAttributeAssignedDomainEvent(SkuId skuId, AttributeDefinitionId attributeDefinitionId, string attributeCode, int sequence)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        AttributeDefinitionId = attributeDefinitionId;
        AttributeCode = attributeCode;
        Sequence = sequence;
    }
}
