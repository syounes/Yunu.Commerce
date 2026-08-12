using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Categories;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Skus;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// Explicit, hand-written mapping between the Product Aggregate and its MongoDB
/// persistence document. No AutoMapper is used (docs/adr/0001 §9, "prefer explicit
/// mapping"). This mapper reads only the fields required to reconstitute a valid
/// Product; it never reads or writes Product.DomainEvents.
/// </summary>
internal static class ProductDocumentMapper
{
    public static ProductDocument ToDocument(Product product)
    {
        return new ProductDocument
        {
            Id = product.Id.Value,
            Name = product.Name.Value,
            BrandId = product.BrandId.Value,
            CategoryId = product.CategoryId.Value,
            Status = product.Status.ToString(),
            Skus = product.Skus
                .Select(sku => new SkuDocument
                {
                    SkuId = sku.Id.Value,
                    Code = sku.Code.Value,
                    Status = sku.Status.ToString()
                })
                .ToList()
        };
    }

    public static Product ToDomain(ProductDocument document)
    {
        var product = Product.Create(
            new ProductId(document.Id),
            new ProductName(document.Name),
            new BrandId(document.BrandId),
            new CategoryId(document.CategoryId),
            Enum.Parse<ProductStatus>(document.Status));

        foreach (var skuDocument in document.Skus)
        {
            product.AddSku(
                new SkuId(skuDocument.SkuId),
                new SkuCode(skuDocument.Code),
                Enum.Parse<SkuStatus>(skuDocument.Status));
        }

        product.ClearDomainEvents();

        return product;
    }
}
