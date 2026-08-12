namespace Yunu.Commerce.SharedKernel;

/// <summary>
/// Minimal, technology-agnostic marker for a domain event.
/// Intentionally free of any Bounded Context business concept
/// (docs/architecture/06-solution-structure.md §10).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
