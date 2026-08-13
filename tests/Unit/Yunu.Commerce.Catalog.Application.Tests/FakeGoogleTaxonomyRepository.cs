using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for IGoogleTaxonomyRepository. Only GetByIdAsync is
/// exercised by CreateProductHandlerTests; other members throw because
/// they are not part of the CreateProduct use case.
/// </summary>
internal sealed class FakeGoogleTaxonomyRepository : IGoogleTaxonomyRepository
{
    private readonly Dictionary<int, GoogleTaxonomyCategoryResponse> _categories = new();

    public void AddCategory(GoogleTaxonomyCategoryResponse category)
    {
        _categories[category.GoogleCategoryId] = category;
    }

    public Task<GoogleTaxonomyCategoryResponse?> GetByIdAsync(int googleCategoryId, CancellationToken cancellationToken)
    {
        _categories.TryGetValue(googleCategoryId, out var category);
        return Task.FromResult(category);
    }

    public Task<GoogleTaxonomySynchronizationResult> SynchronizeAsync(
        IReadOnlyCollection<GoogleTaxonomyCategoryItem> categories,
        string sourceLanguage,
        string sourceUrl,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Not used by CreateProduct tests.");
    }

    public Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Not used by CreateProduct tests.");
    }

    public Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> GetAncestorsAsync(
        int googleCategoryId,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Not used by CreateProduct tests.");
    }
}
