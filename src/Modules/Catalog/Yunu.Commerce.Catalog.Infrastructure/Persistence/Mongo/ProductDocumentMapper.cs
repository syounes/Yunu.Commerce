using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Families;
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
            FamilyId = product.FamilyId?.Value,
            GoogleCategory = new GoogleCategoryDocument
            {
                Id = product.GoogleCategory.Id,
                Path = product.GoogleCategory.Path
            },
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
            document.FamilyId is { } familyId ? new FamilyId(familyId) : (FamilyId?)null,
            new GoogleCategoryReference(document.GoogleCategory.Id, document.GoogleCategory.Path),
            Enum.Parse<ProductStatus>(document.Status));

        product.ClearDomainEvents();

        return product;
    }
}
