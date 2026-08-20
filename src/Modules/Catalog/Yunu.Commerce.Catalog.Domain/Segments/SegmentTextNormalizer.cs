using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Deterministic text normalizer for the Segments bounded context. Mirrors
/// the normalization algorithm used by
/// <see cref="Yunu.Commerce.Catalog.Domain.Brands.Brand.ComputeNormalizedName"/>
/// but is intentionally an independent, Segments-owned implementation so that
/// SegmentDefinition (and, in a future step, SegmentOption) do not depend on
/// the Brands bounded context. Non-AI, culture-safe, consistent everywhere:
/// trim, remove diacritics, collapse whitespace, invariant-uppercase.
/// </summary>
public static class SegmentTextNormalizer
{
    public static string Normalize(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var trimmed = text.Trim();
        var noDiacritics = RemoveDiacritics(trimmed);
        var normalizedWhitespace = Regex.Replace(noDiacritics, "\\s+", " ");
        return normalizedWhitespace.ToUpperInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
