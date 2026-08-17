namespace Yunu.Commerce.Catalog.Domain.Brands;

/// <summary>
/// Human-display Brand name Value Object (docs/domains/catalog.md \u00a712).
/// Only the invariants explicitly supported by the documentation are enforced:
/// non-null, non-empty, non-whitespace, and trimmed. Casing/accents are
/// preserved as provided; no maximum length is imposed because none is documented.
/// </summary>
public sealed record BrandName
{
    public string Value { get; }

    public BrandName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("BrandName is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
