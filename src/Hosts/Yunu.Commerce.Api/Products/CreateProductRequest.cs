namespace Yunu.Commerce.Api.Products;

/// <summary>
/// HTTP request contract for creating a Product. ProductId is intentionally
/// absent: identity is generated inside Catalog.Application.
/// </summary>
public sealed class CreateProductRequest
{
    public required string Name { get; init; }

    public required Guid BrandId { get; init; }

    public required Guid CategoryId { get; init; }
}
