namespace Yunu.Commerce.Catalog.Domain.Products.Skus;

/// <summary>
/// Sku code Value Object (docs/domains/catalog.md §46).
/// Only the invariants explicitly supported by the documentation are enforced:
/// non-null, non-empty, non-whitespace, and trimmed. No case normalization is
/// applied because none is documented; equality is exact and case-sensitive.
/// </summary>
public sealed record SkuCode
{
    public string Value { get; }

    public SkuCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Sku code cannot be null, empty or whitespace.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
