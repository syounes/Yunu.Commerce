namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Strongly typed, database-independent identity for a Product (docs/domains/catalog.md §7).
/// </summary>
public readonly record struct ProductId
{
    public Guid Value { get; }

    public ProductId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ProductId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static ProductId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
