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
/// </summary>
public sealed class GetProductByIdHandler
{
    private readonly IProductRepository _productRepository;
    private readonly ISkuRepository _skuRepository;

    public GetProductByIdHandler(IProductRepository productRepository, ISkuRepository skuRepository)
    {
        _productRepository = productRepository;
        _skuRepository = skuRepository;
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

        return new ProductResponse
        {
            ProductId = product.Id.Value,
            Name = product.Name.Value,
            Description = product.Description,
            BrandId = product.BrandId.Value,
            CategoryId = product.CategoryId.Value,
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
