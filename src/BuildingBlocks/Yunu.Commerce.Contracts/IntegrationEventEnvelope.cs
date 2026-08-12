namespace Yunu.Commerce.Contracts;

/// <summary>
/// Platform-level technical envelope for integration events exchanged between Bounded Contexts
/// (docs/architecture/06-solution-structure.md §11, .github/copilot-instructions.md §14).
/// Business-specific event payloads are owned by their respective module Contracts project.
/// </summary>
public sealed class IntegrationEventEnvelope
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required string AggregateId { get; init; }

    public required string AggregateType { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required int SchemaVersion { get; init; }

    public required string Source { get; init; }

    public required object Data { get; init; }
}
