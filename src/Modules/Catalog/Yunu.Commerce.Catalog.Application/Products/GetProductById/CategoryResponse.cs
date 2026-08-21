namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Dedicated read model for a Product's enriched Canonical Taxonomy
/// classification (docs task: "Canonical Taxonomy + Segments Domain" §33).
/// NormalizedName is intentionally not exposed here.
/// </summary>
public sealed class CategoryResponse
{
    public required long Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required string Path { get; init; }
}
