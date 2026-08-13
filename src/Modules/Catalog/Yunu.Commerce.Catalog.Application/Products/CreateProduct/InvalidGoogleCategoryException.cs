namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Raised when CreateProduct is given a GoogleCategoryId that does not resolve
/// to an existing, active, leaf category in the Google Product Taxonomy
/// (docs task: "Google category resolution during Product creation").
/// The API layer translates this into HTTP 400.
/// </summary>
public sealed class InvalidGoogleCategoryException : Exception
{
    public InvalidGoogleCategoryException(string message) : base(message)
    {
    }
}
