using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Application.Products.CreateProduct;

/// <summary>
/// Orchestrates creation of a new Product Aggregate (docs/domains/catalog.md §49,
/// docs/adr/0001-use-ddd-clean-hexagonal.md §8). Business invariants are enforced
/// entirely by Catalog.Domain (Product.Create and its Value Objects); this handler
/// performs only mapping, Canonical Taxonomy resolution, Segment resolution and
/// persistence orchestration.
///
/// Product is classified by a CanonicalTaxonomyNode (docs task: "Canonical
/// Taxonomy + Segments Domain" §13, §27). The canonical node is resolved from
/// <see cref="ICanonicalTaxonomyRepository"/> (backed by SQL Server) BEFORE the
/// Product Aggregate is constructed; the Domain never performs this lookup
/// itself. Only an existing, leaf node is accepted; leaf-ness is derived from
/// the absence of children, since CanonicalTaxonomyNode does not persist an
/// IsLeaf flag. External taxonomies such as the Google Product Taxonomy are
/// external mappings/catalogs used by other flows and are not the Product
/// Aggregate's canonical classification.
///
/// Domain Events raised during creation (ProductCreatedDomainEvent) remain in the
/// Aggregate's event collection and are not dispatched or cleared at this phase;
/// no Integration Event or Outbox mechanism exists yet.
/// </summary>
public sealed class CreateProductHandler
{
    private static readonly IReadOnlyCollection<SegmentAssignmentScope> AllowedScopes = new[]
    {
        SegmentAssignmentScope.Product,
        SegmentAssignmentScope.ProductWithSkuOverride
    };

    private readonly IProductRepository _productRepository;
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly SegmentAssignmentResolver _segmentAssignmentResolver;

    public CreateProductHandler(
        IProductRepository productRepository,
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        SegmentAssignmentResolver segmentAssignmentResolver)
    {
        _productRepository = productRepository;
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _segmentAssignmentResolver = segmentAssignmentResolver;
    }

    public async Task<CreateProductResult> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var productId = ProductId.New();
        var name = new ProductName(command.Name);
        var brandId = command.BrandId is { } brandIdValue ? new BrandId(brandIdValue) : (BrandId?)null;

        var canonicalTaxonomyNodeId = await ResolveCanonicalTaxonomyNodeAsync(command.CanonicalTaxonomyNodeId, cancellationToken);

        var resolvedSegments = await _segmentAssignmentResolver.ResolveAsync(command.Segments, AllowedScopes, cancellationToken);

        var product = Product.Create(productId, name, command.Description, brandId, canonicalTaxonomyNodeId);

        foreach (var resolvedSegment in resolvedSegments)
        {
            product.AssignSegment(resolvedSegment.SegmentDefinitionId, resolvedSegment.SegmentCode, resolvedSegment.Options);
        }

        await _productRepository.AddAsync(product, cancellationToken);

        return new CreateProductResult
        {
            ProductId = productId.Value
        };
    }

    private async Task<CanonicalTaxonomyNodeId> ResolveCanonicalTaxonomyNodeAsync(long canonicalTaxonomyNodeId, CancellationToken cancellationToken)
    {
        var id = new CanonicalTaxonomyNodeId(canonicalTaxonomyNodeId);

        var node = await _canonicalTaxonomyRepository.GetByIdAsync(id, cancellationToken);

        if (node is null)
        {
            throw new ArgumentException($"Canonical Taxonomy node '{canonicalTaxonomyNodeId}' does not exist.", nameof(canonicalTaxonomyNodeId));
        }

        if (node.Status != CanonicalTaxonomyNodeStatus.Active)
        {
            throw new ArgumentException($"Canonical Taxonomy node '{canonicalTaxonomyNodeId}' is not active.", nameof(canonicalTaxonomyNodeId));
        }

        if (await _canonicalTaxonomyRepository.HasChildrenAsync(id, cancellationToken))
        {
            throw new ArgumentException($"Canonical Taxonomy node '{canonicalTaxonomyNodeId}' is not a leaf node.", nameof(canonicalTaxonomyNodeId));
        }

        return id;
    }
}
