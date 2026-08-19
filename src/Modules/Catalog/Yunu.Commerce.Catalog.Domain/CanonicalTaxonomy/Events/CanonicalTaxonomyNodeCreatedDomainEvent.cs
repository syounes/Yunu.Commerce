using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy.Events;

/// <summary>
/// Raised when a new Canonical Taxonomy node is created (docs task:
/// "Canonical Taxonomy + Segments Domain").
/// </summary>
public sealed class CanonicalTaxonomyNodeCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public CanonicalTaxonomyNodeId CanonicalTaxonomyNodeId { get; }

    public CanonicalTaxonomyNodeCreatedDomainEvent(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        CanonicalTaxonomyNodeId = canonicalTaxonomyNodeId;
    }
}
