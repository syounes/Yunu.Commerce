using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.ProductProposals.Events;

/// <summary>
/// Raised when a new <see cref="ProductProposal"/> is created (docs task:
/// "Catalog intent resolution orchestration" - proposal persistence).
/// </summary>
public sealed class ProductProposalCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public ProductProposalId ProductProposalId { get; }

    public ProductProposalCreatedDomainEvent(ProductProposalId productProposalId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        ProductProposalId = productProposalId;
    }
}
