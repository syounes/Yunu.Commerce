namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Input for creating a new Product (docs/domains/catalog.md §49).
/// ProductId is intentionally absent: identity is assigned internally by
/// <see cref="CreateProductHandler"/> via ProductId.New(). External system
/// identifiers (ERP, supplier, marketplace) are out of scope for this command
/// and will be modeled separately by a future ExternalReference use case.
/// </summary>
public sealed class CreateProductCommand
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required Guid BrandId { get; init; }

    public required Guid CategoryId { get; init; }
}
