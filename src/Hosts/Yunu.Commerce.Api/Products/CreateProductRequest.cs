namespace Yunu.Commerce.Api.Products;

/// <summary>
/// HTTP request contract for creating a Product. ProductId is intentionally
/// absent: identity is generated inside Catalog.Application.
///
/// BrandId is optional (internal Yunu classification may be assigned later).
/// CanonicalTaxonomyNodeId is required and is the only classification input
/// accepted from callers; the node is always resolved and validated
/// server-side against SQL Server (Catalog.CanonicalTaxonomyNodes) and must
/// never be supplied with a caller-provided path/name/depth. External
/// taxonomies such as the Google Product Taxonomy are not the Product
/// Aggregate's canonical classification.
///
/// Segments are optional, explicit selections (docs task: "Canonical
/// Taxonomy + Segments Domain" §26); the caller supplies only Code and
/// OptionCodes, never SegmentDefinitionId/SegmentOptionId/AssignmentScope/
/// SelectionMode.
/// </summary>
public sealed class CreateProductRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public Guid? BrandId { get; init; }

    public required long CanonicalTaxonomyNodeId { get; init; }

    public IReadOnlyCollection<SegmentSelectionRequest> Segments { get; init; } = Array.Empty<SegmentSelectionRequest>();
}
