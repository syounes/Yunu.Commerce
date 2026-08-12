namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Product name Value Object (docs/domains/catalog.md §46).
/// Only the invariants explicitly supported by the documentation are enforced:
/// non-null, non-empty, non-whitespace, and trimmed. No maximum length is imposed
/// because none is documented.
/// </summary>
public sealed record ProductName
{
    public string Value { get; }

    public ProductName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product name cannot be null, empty or whitespace.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
