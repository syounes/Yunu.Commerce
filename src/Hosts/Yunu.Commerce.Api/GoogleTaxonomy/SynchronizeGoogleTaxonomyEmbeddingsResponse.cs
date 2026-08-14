namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// HTTP response summarizing the outcome of a Google Product Taxonomy
/// embeddings batch synchronization. Never contains the generated vectors.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyEmbeddingsResponse
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
