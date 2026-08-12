using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// Explicit, hand-written mapping between the Sku Aggregate and its MongoDB
/// persistence document (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// No AutoMapper is used (docs/adr/0001 §9, "prefer explicit mapping"). Never
/// reads or writes Sku.DomainEvents.
/// </summary>
internal static class SkuDocumentMapper
{
    public static SkuDocument ToDocument(Sku sku)
    {
        return new SkuDocument
        {
            Id = sku.Id.Value,
            ProductId = sku.ProductId.Value,
            Code = sku.Code.Value,
            Gtin = sku.Gtin,
            Status = sku.Status.ToString()
        };
    }

    public static Sku ToDomain(SkuDocument document)
    {
        var sku = Sku.Create(
            new SkuId(document.Id),
            new ProductId(document.ProductId),
            new SkuCode(document.Code),
            document.Gtin,
            Enum.Parse<SkuStatus>(document.Status));

        sku.ClearDomainEvents();

        return sku;
    }
}
