using Yunu.Commerce.Catalog.Application.SegmentCatalog;

namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Input for creating a new Product (docs/domains/catalog.md §49).
/// ProductId is intentionally absent: identity is assigned internally by
/// <see cref="CreateProductHandler"/> via ProductId.New(). External system
/// identifiers (ERP, supplier, marketplace) are out of scope for this command
/// and will be modeled separately by a future ExternalReference use case.
///
/// Classification modeling decision (docs task: "Canonical Taxonomy +
/// Segments Domain" §25): BrandId (the internal Yunu classification
/// reference) is optional, because internal classification may be assigned
/// after creation. CanonicalTaxonomyNodeId is required and is resolved by
/// <see cref="CreateProductHandler"/> against Catalog.CanonicalTaxonomyNodes
/// before the Product Aggregate is created; only the node id is accepted
/// here, never a caller-supplied path/depth.
///
/// Segments are optional, explicit selections resolved and validated by
/// <see cref="CreateProductHandler"/> against SQL Server before the Product
/// Aggregate assigns them (docs task §26-§28).
/// </summary>
public sealed class CreateProductCommand
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public Guid? BrandId { get; init; }

    public required long CanonicalTaxonomyNodeId { get; init; }

    public IReadOnlyCollection<SegmentSelectionInput> Segments { get; init; } = Array.Empty<SegmentSelectionInput>();
}
