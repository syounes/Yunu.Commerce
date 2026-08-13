namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Outcome returned by <see cref="SynchronizeGoogleTaxonomyHandler"/> to the API layer.
/// </summary>
public sealed class SynchronizeGoogleTaxonomyResult
{
    public required string Status { get; init; }

    public required int TotalCategories { get; init; }

    public required int Inserted { get; init; }

    public required int Updated { get; init; }

    public required int Deactivated { get; init; }

    public required DateTime StartedAtUtc { get; init; }

    public required DateTime CompletedAtUtc { get; init; }
}
