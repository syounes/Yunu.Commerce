namespace Yunu.Commerce.Api.SegmentEmbeddings;

/// <summary>
/// HTTP response summarizing the outcome of a Segment embeddings batch
/// synchronization. Never contains the generated vectors.
/// </summary>
public sealed class SynchronizeSegmentEmbeddingsResponse
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
