using Yunu.Commerce.SharedKernel;

namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy.Events;

/// <summary>
/// Raised when a leaf Canonical Taxonomy node is updated (docs task:
/// "Canonical Taxonomy + Segments Domain").
/// </summary>
public sealed class CanonicalTaxonomyNodeUpdatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public CanonicalTaxonomyNodeId CanonicalTaxonomyNodeId { get; }

    public CanonicalTaxonomyNodeUpdatedDomainEvent(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId)
    {
        EventId = Guid.NewGuid();
        OccurredAtUtc = DateTimeOffset.UtcNow;
        CanonicalTaxonomyNodeId = canonicalTaxonomyNodeId;
    }
}
