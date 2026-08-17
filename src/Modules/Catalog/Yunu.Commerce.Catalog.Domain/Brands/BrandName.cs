namespace Yunu.Commerce.Catalog.Domain.Brands;

/// <summary>
/// Human-display Brand name Value Object.
/// </summary>
public sealed class BrandName
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
