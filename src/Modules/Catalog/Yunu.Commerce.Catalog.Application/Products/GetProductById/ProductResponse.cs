namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Dedicated read model for a Product, decoupled from the Domain Aggregate
/// (docs/domains/catalog.md §51). API responses must not expose Aggregate
/// internals directly.
/// </summary>
public sealed class ProductResponse
{
    public required Guid ProductId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public Guid? BrandId { get; init; }

    public required CategoryResponse Category { get; init; }

    public required IReadOnlyCollection<SegmentAssignmentResponse> Segments { get; init; }

    public required string Status { get; init; }

    public required IReadOnlyCollection<SkuResponse> Skus { get; init; }
}
