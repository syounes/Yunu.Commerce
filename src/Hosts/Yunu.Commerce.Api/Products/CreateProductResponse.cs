namespace Yunu.Commerce.Api.Products;

/// <summary>
/// HTTP response contract returned after a Product is created.
/// </summary>
public sealed class CreateProductResponse
{
    public required Guid ProductId { get; init; }
}
