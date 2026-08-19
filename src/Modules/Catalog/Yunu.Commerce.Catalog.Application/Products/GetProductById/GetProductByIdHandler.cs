using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Orchestrates retrieval of a Product by identity and maps it to a dedicated
/// read model (docs/domains/catalog.md §50-51). Returns null when the Product
/// does not exist; translation to an HTTP-specific outcome (e.g. 404) belongs
/// to the future API host, not to Application (docs/adr/0001 §34).
///
/// Product and Sku are independent Aggregates (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// This handler composes both at the read-model level so the existing API
/// contract (Product + Skus) is preserved without coupling the Aggregates.
///
/// The Product's Canonical Taxonomy classification and Segment assignments
/// are enriched from SQL Server reference data (docs task: "Canonical
/// Taxonomy + Segments Domain" §32-§33); the Product Aggregate itself only
/// stores resolved identities/codes.
/// </summary>
public sealed class GetProductByIdHandler
{
    private readonly IProductRepository _productRepository;
    private readonly ISkuRepository _skuRepository;
    private readonly ICanonicalTaxonomyRepository _canonicalTaxonomyRepository;
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetProductByIdHandler(
        IProductRepository productRepository,
        ISkuRepository skuRepository,
        ICanonicalTaxonomyRepository canonicalTaxonomyRepository,
        ISegmentCatalogRepository segmentCatalogRepository)
    {
        _productRepository = productRepository;
        _skuRepository = skuRepository;
        _canonicalTaxonomyRepository = canonicalTaxonomyRepository;
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public async Task<ProductResponse?> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var productId = new ProductId(query.ProductId);

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var skus = await _skuRepository.GetByProductIdAsync(productId, cancellationToken);

        var categoryNode = await _canonicalTaxonomyRepository.GetByIdAsync(product.CanonicalTaxonomyNodeId, cancellationToken);

        var category = new CategoryResponse
        {
            Id = product.CanonicalTaxonomyNodeId.Value,
            Code = categoryNode?.Code ?? string.Empty,
            Name = categoryNode?.Name ?? string.Empty,
            Path = categoryNode?.Path ?? string.Empty,
            Source = categoryNode?.Source.ToString() ?? string.Empty
        };

        var segments = new List<SegmentAssignmentResponse>();

        foreach (var assignment in product.SegmentAssignments)
        {
            var definition = await _segmentCatalogRepository.GetDefinitionByIdAsync(assignment.SegmentDefinitionId.Value, cancellationToken);

            var options = new List<SegmentOptionAssignmentResponse>();

            foreach (var option in assignment.Options)
            {
                var optionResponse = await _segmentCatalogRepository.GetOptionAsync(assignment.SegmentDefinitionId.Value, option.OptionCode, cancellationToken);

                options.Add(new SegmentOptionAssignmentResponse
                {
                    Code = option.OptionCode,
                    Name = optionResponse?.Name ?? option.OptionCode
                });
            }

            segments.Add(new SegmentAssignmentResponse
            {
                Code = assignment.SegmentCode,
                Name = definition?.Name ?? assignment.SegmentCode,
                AssignmentScope = definition?.AssignmentScope ?? string.Empty,
                Options = options
            });
        }

        return new ProductResponse
        {
            ProductId = product.Id.Value,
            Name = product.Name.Value,
            Description = product.Description,
            BrandId = product.BrandId?.Value,
            Category = category,
            Segments = segments,
            Status = product.Status.ToString(),
            Skus = skus
                .Select(sku => new SkuResponse
                {
                    SkuId = sku.Id.Value,
                    Code = sku.Code.Value,
                    Status = sku.Status.ToString()
                })
                .ToArray()
        };
    }
}
