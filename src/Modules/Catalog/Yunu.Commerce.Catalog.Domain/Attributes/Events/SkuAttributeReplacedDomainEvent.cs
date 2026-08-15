using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Attributes.Events;

/// <summary>
/// Raised when an existing Sku attribute assignment is explicitly replaced
/// with a different effective value (docs task: "SKU attribute foundation").
/// </summary>
public sealed class SkuAttributeReplacedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public SkuId SkuId { get; }

    public AttributeDefinitionId AttributeDefinitionId { get; }

    public string AttributeCode { get; }

    public int Sequence { get; }

    public SkuAttributeReplacedDomainEvent(SkuId skuId, AttributeDefinitionId attributeDefinitionId, string attributeCode, int sequence)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        SkuId = skuId;
        AttributeDefinitionId = attributeDefinitionId;
        AttributeCode = attributeCode;
        Sequence = sequence;
    }
}
