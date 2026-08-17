using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Yunu.Commerce.Catalog.Domain.Brands;

/// <summary>
/// Canonical, persisted Brand code. Immutable Value Object.
/// Rules:
/// - required
/// - trimmed
/// - uppercase
/// - only A-Z and 0-9
/// - length: 2..12
/// </summary>
public sealed class BrandCode
{
    private static readonly Regex ValidCode = new("^[A-Z0-9]{2,12}$", RegexOptions.Compiled);

    public string Value { get; }

    public BrandCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("BrandCode is required.", nameof(value));
        }

        var trimmed = value.Trim();

        // Normalize: remove diacritics and uppercase
        var normalized = RemoveDiacritics(trimmed).ToUpperInvariant();

        // Do not remove spaces — spaces are invalid and will fail validation.

        if (!ValidCode.IsMatch(normalized))
        {
            throw new ArgumentException($"BrandCode '{value}' is invalid. Allowed format: {ValidCode}", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
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
