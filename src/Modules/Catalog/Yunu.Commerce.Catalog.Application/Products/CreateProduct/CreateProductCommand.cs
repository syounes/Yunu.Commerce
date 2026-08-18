namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Input for creating a new Product (docs/domains/catalog.md §49).
/// ProductId is intentionally absent: identity is assigned internally by
/// <see cref="CreateProductHandler"/> via ProductId.New(). External system
/// identifiers (ERP, supplier, marketplace) are out of scope for this command
/// and will be modeled separately by a future ExternalReference use case.
///
/// Classification modeling decision: BrandId (the internal Yunu
/// classification reference) is optional, because internal classification may be
/// assigned after creation. GoogleCategoryId is required and is resolved by
/// <see cref="CreateProductHandler"/> against the canonical Google Product
/// Taxonomy before the Product Aggregate is created; only the taxonomy id is
/// accepted here, never a caller-supplied path/fullPath.
/// </summary>
public sealed class CreateProductCommand
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public Guid? BrandId { get; init; }

    public required int GoogleCategoryId { get; init; }
}
