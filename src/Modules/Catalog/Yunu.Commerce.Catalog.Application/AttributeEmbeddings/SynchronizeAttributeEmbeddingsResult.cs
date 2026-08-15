namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Aggregate outcome of a SKU attribute embeddings batch synchronization.
/// Provider-agnostic; never contains the generated vectors.
///
/// Invariant (barring cancellation): DefinitionsRead + OptionsRead =
/// Generated + Updated (unchanged-but-touched) + Skipped + Failed, per the
/// per-item processing loop.
/// </summary>
public sealed class SynchronizeAttributeEmbeddingsResult
{
    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required int DefinitionsRead { get; init; }

    public required int OptionsRead { get; init; }

    public required int Generated { get; init; }

    public required int Updated { get; init; }

    public required int Skipped { get; init; }

    public required int Deactivated { get; init; }

    public required int Failed { get; init; }

    public required DateTime StartedAtUtc { get; init; }

    public required DateTime CompletedAtUtc { get; init; }

    public required long DurationMilliseconds { get; init; }
}
