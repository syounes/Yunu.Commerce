using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// Explicit, hand-written mapping between the Product Aggregate and its MongoDB
/// persistence document. No AutoMapper is used (docs/adr/0001 §9, "prefer explicit
/// mapping"). This mapper reads only the fields required to reconstitute a valid
/// Product; it never reads or writes Product.DomainEvents.
///
/// Sku is no longer part of Product persistence (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// </summary>
internal static class ProductDocumentMapper
{
    public static ProductDocument ToDocument(Product product)
    {
        return new ProductDocument
        {
            Id = product.Id.Value,
            Name = product.Name.Value,
            Description = product.Description,
            BrandId = product.BrandId?.Value,
            CanonicalTaxonomyNodeId = product.CanonicalTaxonomyNodeId.Value,
            SegmentAssignments = product.SegmentAssignments.Select(sa => new SegmentAssignmentDocument
            {
                SegmentDefinitionId = sa.SegmentDefinitionId.Value,
                SegmentCode = sa.SegmentCode,
                Options = sa.Options.Select(o => new SegmentOptionSelectionDocument
                {
                    SegmentOptionId = o.SegmentOptionId.Value,
                    OptionCode = o.OptionCode
                }).ToList()
            }).ToList(),
            Status = product.Status.ToString()
        };
    }

    public static Product ToDomain(ProductDocument document)
    {
        var product = Product.Create(
            new ProductId(document.Id),
            new ProductName(document.Name),
            document.Description,
            document.BrandId is { } brandId ? new BrandId(brandId) : (BrandId?)null,
            new CanonicalTaxonomyNodeId(document.CanonicalTaxonomyNodeId),
            Enum.Parse<ProductStatus>(document.Status));

        product.ClearDomainEvents();

        return product;
    }

}
