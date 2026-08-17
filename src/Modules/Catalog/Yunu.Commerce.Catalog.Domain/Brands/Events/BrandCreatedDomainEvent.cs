using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.Brands.Events;

public sealed class BrandCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public BrandId BrandId { get; }

    public BrandCreatedDomainEvent(BrandId brandId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        BrandId = brandId;
    }
}
