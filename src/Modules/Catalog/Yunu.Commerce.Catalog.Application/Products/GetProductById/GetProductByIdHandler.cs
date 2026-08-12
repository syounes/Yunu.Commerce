using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Products.GetProductById;

/// <summary>
/// Orchestrates retrieval of a Product by identity and maps it to a dedicated
/// read model (docs/domains/catalog.md §50-51). Returns null when the Product
/// does not exist; translation to an HTTP-specific outcome (e.g. 404) belongs
/// to the future API host, not to Application (docs/adr/0001 §34).
/// </summary>
public sealed class GetProductByIdHandler
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductResponse?> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var productId = new ProductId(query.ProductId);

        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        return new ProductResponse
        {
            ProductId = product.Id.Value,
            Name = product.Name.Value,
            BrandId = product.BrandId.Value,
            CategoryId = product.CategoryId.Value,
            Status = product.Status.ToString(),
            Skus = product.Skus
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
