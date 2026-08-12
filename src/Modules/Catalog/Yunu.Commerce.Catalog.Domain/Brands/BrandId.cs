namespace Yunu.Commerce.Catalog.Domain.Brands;

/// <summary>
/// Strongly typed identifier referencing a Brand owned by another Bounded Context boundary
/// within Catalog (docs/domains/catalog.md §12). Catalog references Brand by identity only.
/// </summary>
public readonly record struct BrandId
{
    public Guid Value { get; }

    public BrandId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("BrandId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static BrandId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
