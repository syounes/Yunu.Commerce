namespace Yunu.Commerce.Catalog.Domain.Families;

/// <summary>
/// Strongly typed identifier referencing a Family within the internal Yunu
/// hierarchy (Department → Category → SubCategory → Family → Product).
/// Catalog references Family by identity only; Product.FamilyId is optional
/// because internal Yunu classification may be assigned after creation
/// (docs/domains/catalog.md).
/// </summary>
public readonly record struct FamilyId
{
    public Guid Value { get; }

    public FamilyId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("FamilyId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static FamilyId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
