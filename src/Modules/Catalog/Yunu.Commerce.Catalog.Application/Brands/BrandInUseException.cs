namespace Yunu.Commerce.Catalog.Application.Brands;

/// <summary>
/// Thrown when an UPDATE or DELETE is attempted against a Brand that is
/// currently referenced by at least one Product (docs task: "Canonical
/// Taxonomy + Segments Domain" §36).
/// </summary>
public sealed class BrandInUseException : Exception
{
    public BrandInUseException(string message) : base(message)
    {
    }
}
