using System.Globalization;
using System.Text.RegularExpressions;

namespace Yunu.Commerce.Catalog.Domain.Brands;

/// <summary>
/// Canonical, persisted Brand code. Immutable Value Object (docs/domains/catalog.md §12).
/// BrandCode is already-canonical input: the Value Object validates it and never
/// transforms/normalizes arbitrary human text (no accent stripping, no implicit
/// uppercasing). Deterministic generation of a canonical code from human text is
/// an Application-level concern, not a Domain responsibility.
/// Rules:
/// - required
/// - only A-Z and 0-9 (uppercase only, as provided)
/// - length: 2..12
/// </summary>
public sealed record BrandCode
{
    private static readonly Regex ValidCode = new("^[A-Z0-9]{2,12}$", RegexOptions.Compiled);

    public string Value { get; }

    public BrandCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("BrandCode is required.", nameof(value));
        }

        if (!ValidCode.IsMatch(value))
        {
            throw new ArgumentException($"BrandCode '{value}' is invalid. Allowed format: {ValidCode}", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
