namespace Yunu.Commerce.Catalog.Domain.Categories;

/// <summary>
/// Strongly typed identifier referencing a Category (docs/domains/catalog.md §10).
/// Catalog references Category by identity only in this phase.
/// </summary>
public readonly record struct CategoryId
{
    public Guid Value { get; }

    public CategoryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("CategoryId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static CategoryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
