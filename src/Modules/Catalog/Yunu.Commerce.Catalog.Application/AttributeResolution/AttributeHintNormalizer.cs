using System.Globalization;
using System.Text;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Normalizes hint text for exact-match comparison (docs task: "Semantic
/// attribute hint resolution", Etapa A). Trims, lowercases (invariant
/// culture) and strips diacritics, but never removes digits, units, GTIN,
/// MPN or brand-relevant characters. The original (unnormalized) text is
/// always preserved separately by callers.
/// </summary>
internal static class AttributeHintNormalizer
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
