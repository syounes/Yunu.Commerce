using Yunu.Commerce.Catalog.Application.Skus.GetSkuById;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;

/// <summary>
/// Orchestrates retrieval of all Skus for a given Product identity
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md). Reuses
/// <see cref="SkuDetailsResponse"/> as the shared Sku read model.
/// </summary>
public sealed class GetSkusByProductIdHandler
{
    private readonly ISkuRepository _skuRepository;
    private readonly IProductRepository _productRepository;

    public GetSkusByProductIdHandler(ISkuRepository skuRepository, IProductRepository productRepository)
    {
        _skuRepository = skuRepository;
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyCollection<SkuDetailsResponse>> HandleAsync(GetSkusByProductIdQuery query, CancellationToken cancellationToken)
    {
        var productId = new ProductId(query.ProductId);

        var skus = await _skuRepository.GetByProductIdAsync(productId, cancellationToken);
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        var productStatus = product?.Status ?? ProductStatus.Draft;

        return skus
            .Select(sku => new SkuDetailsResponse
            {
                SkuId = sku.Id.Value,
                ProductId = sku.ProductId.Value,
                Code = sku.Code.Value,
                Gtin = sku.Gtin,
                Status = sku.Status.ToString(),
                CommerciallyEligible = CommercialEligibility.CommercialEligibilityPolicy.IsEligible(productStatus, sku.Status),
                Attributes = sku.Attributes.Select(SkuAttributeResponseMapper.ToResponse).ToArray()
            })
            .ToArray();
    }
}
