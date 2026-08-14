namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

/// <summary>
/// Reconstructs parent/child relationships from Google's flat, path-based
/// taxonomy rows and validates the resulting structure before it is allowed
/// to reach persistence (docs: "Google gives complete paths").
/// </summary>
public static class GoogleTaxonomyHierarchyBuilder
{
    private const string PathSeparator = " > ";

    /// <summary>
    /// Builds the full <see cref="GoogleTaxonomyCategoryItem"/> collection with
    /// resolved parents, computed levels and computed leaf status.
    /// Throws <see cref="GoogleTaxonomyValidationException"/> when the feed is
    /// empty or structurally invalid.
    /// </summary>
    public static IReadOnlyCollection<GoogleTaxonomyCategoryItem> Build(
        IReadOnlyCollection<ParsedGoogleTaxonomyRow> rows)
    {
        if (rows.Count == 0)
        {
            throw new GoogleTaxonomyValidationException(
                "The downloaded Google taxonomy is empty or contains no valid rows.");
        }

        var duplicateIds = rows
            .GroupBy(r => r.GoogleCategoryId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new GoogleTaxonomyValidationException(
                $"Duplicate Google category IDs found: {string.Join(", ", duplicateIds)}.");
        }

        var duplicatePaths = rows
            .GroupBy(r => r.FullPath, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicatePaths.Length > 0)
        {
            throw new GoogleTaxonomyValidationException(
                $"Duplicate Google taxonomy paths found: {string.Join(", ", duplicatePaths)}.");
        }

        var idByPath = rows.ToDictionary(r => r.FullPath, r => r.GoogleCategoryId, StringComparer.Ordinal);

        var parentByChild = new Dictionary<int, int?>();

        foreach (var row in rows)
        {
            var lastSeparatorIndex = row.FullPath.LastIndexOf(PathSeparator, StringComparison.Ordinal);

            if (lastSeparatorIndex < 0)
            {
                parentByChild[row.GoogleCategoryId] = null;
                continue;
            }

            var parentPath = row.FullPath[..lastSeparatorIndex];

            if (!idByPath.TryGetValue(parentPath, out var parentGoogleCategoryId))
            {
                throw new GoogleTaxonomyValidationException(
                    $"Category '{row.FullPath}' references a parent path '{parentPath}' that does not exist in the downloaded taxonomy.");
            }

            if (parentGoogleCategoryId == row.GoogleCategoryId)
            {
                throw new GoogleTaxonomyValidationException(
                    $"Category {row.GoogleCategoryId} references itself as its own parent.");
            }

            parentByChild[row.GoogleCategoryId] = parentGoogleCategoryId;
        }

        DetectCycles(parentByChild);

        var childCategoryIds = parentByChild.Values
            .Where(parentId => parentId.HasValue)
            .Select(parentId => parentId!.Value)
            .ToHashSet();

        return rows
            .Select(row => new GoogleTaxonomyCategoryItem(
                GoogleCategoryId: row.GoogleCategoryId,
                ParentGoogleCategoryId: parentByChild[row.GoogleCategoryId],
                Name: row.Name,
                FullPath: row.FullPath,
                Level: row.Level,
                IsLeaf: !childCategoryIds.Contains(row.GoogleCategoryId)))
            .ToArray();
    }

    private static void DetectCycles(Dictionary<int, int?> parentByChild)
    {
        var visitState = new Dictionary<int, int>();

        foreach (var categoryId in parentByChild.Keys)
        {
            VisitForCycleDetection(categoryId, parentByChild, visitState);
        }
    }

    private static void VisitForCycleDetection(
        int categoryId,
        Dictionary<int, int?> parentByChild,
        Dictionary<int, int> visitState)
    {
        if (visitState.TryGetValue(categoryId, out var state))
        {
            if (state == 1)
            {
                throw new GoogleTaxonomyValidationException(
                    $"A cycle was detected in the Google taxonomy hierarchy involving category {categoryId}.");
            }

            return;
        }

        visitState[categoryId] = 1;

        if (parentByChild.TryGetValue(categoryId, out var parentId) && parentId.HasValue)
        {
            VisitForCycleDetection(parentId.Value, parentByChild, visitState);
        }

        visitState[categoryId] = 2;
    }
}
