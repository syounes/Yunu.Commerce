namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Input for retrieving a Product by its canonical identity (docs/domains/catalog.md §50).
/// </summary>
public sealed class GetProductByIdQuery
{
    public required Guid ProductId { get; init; }
}
