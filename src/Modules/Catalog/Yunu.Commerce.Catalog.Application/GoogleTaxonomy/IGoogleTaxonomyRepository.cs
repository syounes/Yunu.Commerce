using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Port for persisting a synchronized Google Product Taxonomy snapshot.
/// Infrastructure implements this against SQL Server. The Application layer
/// never references SqlConnection, Dapper or any other vendor-specific type.
/// </summary>
public interface IGoogleTaxonomyRepository
{
    Task<GoogleTaxonomyPersistenceResult> SynchronizeAsync(
        IReadOnlyCollection<GoogleTaxonomyCategoryItem> categories,
        string sourceLanguage,
        string sourceUrl,
        DateTime importedAtUtc,
        CancellationToken cancellationToken);

    Task<GoogleTaxonomyCategoryResponse?> GetByIdAsync(
        int googleCategoryId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> GetAncestorsAsync(
        int googleCategoryId,
        CancellationToken cancellationToken);
}
