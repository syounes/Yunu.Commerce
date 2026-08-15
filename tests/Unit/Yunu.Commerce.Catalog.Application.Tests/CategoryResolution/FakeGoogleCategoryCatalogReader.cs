using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

/// <summary>
/// Test-only fake for IGoogleCategoryCatalogReader. Backed by simple
/// in-memory collections seeded per test; never touches SQL Server.
/// </summary>
internal sealed class FakeGoogleCategoryCatalogReader : IGoogleCategoryCatalogReader
{
    private readonly List<GoogleCategoryCatalogEntry> _entries = [];

    public void Add(GoogleCategoryCatalogEntry entry) => _entries.Add(entry);

    public Task<IReadOnlyList<GoogleCategoryCatalogEntry>> FindExactMatchesAsync(
        string categoryHint,
        string locale,
        CancellationToken cancellationToken)
    {
        var normalizedHint = CategoryResolutionTestNormalizer.Normalize(categoryHint);

        var matches = _entries
            .Where(e => e.IsActive && (
                CategoryResolutionTestNormalizer.Normalize(e.Name) == normalizedHint ||
                CategoryResolutionTestNormalizer.Normalize(e.FullPath) == normalizedHint ||
                (long.TryParse(categoryHint.Trim(), out var id) && e.GoogleCategoryId == id)))
            .ToArray();

        return Task.FromResult<IReadOnlyList<GoogleCategoryCatalogEntry>>(matches);
    }

    public Task<IReadOnlyList<GoogleCategoryCatalogEntry>> GetByIdsAsync(
        IReadOnlyCollection<long> googleCategoryIds,
        CancellationToken cancellationToken)
    {
        var idSet = new HashSet<long>(googleCategoryIds);

        var matches = _entries
            .Where(e => e.IsActive && idSet.Contains(e.GoogleCategoryId))
            .ToArray();

        return Task.FromResult<IReadOnlyList<GoogleCategoryCatalogEntry>>(matches);
    }
}
