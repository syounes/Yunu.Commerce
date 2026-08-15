using System.Globalization;
using System.Text;

namespace Yunu.Commerce.Catalog.Application.Tests.CategoryResolution;

/// <summary>
/// Test-only mirror of the production case/accent-insensitive normalization
/// used by SqlGoogleCategoryCatalogReader's COLLATE comparisons.
/// </summary>
internal static class CategoryResolutionTestNormalizer
{
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        var decomposed = trimmed.Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
    }
}
