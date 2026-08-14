namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

/// <summary>
/// Aggregate outcome of a Google Taxonomy embeddings batch synchronization.
/// Provider-agnostic; never contains the generated vectors.
///
/// Invariant (barring cancellation): Processed = Generated + Skipped + Failed.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyEmbeddingsResult
{
    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required int TotalCategories { get; init; }

    public required int Processed { get; init; }

    public required int Generated { get; init; }

    public required int Skipped { get; init; }

    public required int Failed { get; init; }

    public required DateTime StartedAtUtc { get; init; }

    public required DateTime CompletedAtUtc { get; init; }

    public required long DurationMilliseconds { get; init; }
}
