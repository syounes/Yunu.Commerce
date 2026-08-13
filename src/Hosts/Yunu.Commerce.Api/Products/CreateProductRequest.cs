namespace Yunu.Commerce.Api.Products;

/// <summary>
/// HTTP request contract for creating a Product. ProductId is intentionally
/// absent: identity is generated inside Catalog.Application.
///
/// BrandId and FamilyId are optional (internal Yunu classification may be
/// assigned later). GoogleCategoryId is required and is the only Google
/// taxonomy input accepted from callers; the canonical path is always
/// resolved server-side from SQL Server and must never be supplied by the
/// caller.
/// </summary>
public sealed class CreateProductRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public Guid? BrandId { get; init; }

    public Guid? FamilyId { get; init; }

    public required int GoogleCategoryId { get; init; }
}
