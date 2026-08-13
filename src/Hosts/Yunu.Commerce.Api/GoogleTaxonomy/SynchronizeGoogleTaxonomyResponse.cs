namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// HTTP response contract returned after a Google Product Taxonomy
/// synchronization completes.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyResponse
{
    public required string Status { get; init; }

    public required int TotalCategories { get; init; }

    public required int Inserted { get; init; }

    public required int Updated { get; init; }

    public required int Deactivated { get; init; }

    public required DateTime StartedAt { get; init; }

    public required DateTime CompletedAt { get; init; }
}
