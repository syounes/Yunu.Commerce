namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Dedicated read model for a Product's denormalized Google category reference
/// (docs/domains/catalog.md - external classification systems). Decoupled from
/// the Domain's <c>GoogleCategoryReference</c> Value Object.
/// </summary>
public sealed class GoogleCategoryResponse
{
    public required int Id { get; init; }

    public required string Path { get; init; }
}
