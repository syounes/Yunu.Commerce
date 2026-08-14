namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

/// <summary>
/// Outcome of a Google Product Taxonomy persistence operation, returned by
/// <see cref="IGoogleTaxonomyRepository.SynchronizeAsync"/> and consumed by the
/// SynchronizeGoogleTaxonomy use case to build the final
/// <see cref="SynchronizeGoogleTaxonomyResult"/>. Distinct from
/// <see cref="SynchronizeGoogleTaxonomyResult"/>: this type only carries
/// persistence counts, while the use case result also carries orchestration
/// details (status, start/completion timestamps).
/// </summary>
public sealed record GoogleTaxonomyPersistenceResult(
    int TotalCategories,
    int Inserted,
    int Updated,
    int Deactivated);
