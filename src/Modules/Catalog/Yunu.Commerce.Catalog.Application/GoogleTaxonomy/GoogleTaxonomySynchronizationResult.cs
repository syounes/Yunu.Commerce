namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Outcome of a Google Product Taxonomy synchronization run, returned by
/// <see cref="IGoogleTaxonomyRepository.SynchronizeAsync"/> and surfaced by the
/// SynchronizeGoogleTaxonomy use case.
/// </summary>
public sealed record GoogleTaxonomySynchronizationResult(
    int TotalCategories,
    int Inserted,
    int Updated,
    int Deactivated);
