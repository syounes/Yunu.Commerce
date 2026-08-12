namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Minimal confirmation returned after a Product is created.
/// </summary>
public sealed class CreateProductResult
{
    public required Guid ProductId { get; init; }
}
