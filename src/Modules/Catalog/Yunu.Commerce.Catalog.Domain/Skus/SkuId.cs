namespace Yunu.Commerce.Catalog.Domain.Skus;

/// <summary>
/// Strongly typed, database-independent identity for a Sku (docs/domains/catalog.md §8).
/// </summary>
public readonly record struct SkuId
{
    public Guid Value { get; }

    public SkuId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("SkuId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static SkuId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
