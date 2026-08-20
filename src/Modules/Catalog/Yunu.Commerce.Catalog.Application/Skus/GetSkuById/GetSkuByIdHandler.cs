using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.GetSkuById;

/// <summary>
/// Orchestrates retrieval of a Sku by identity and maps it to a dedicated read
/// model (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Returns null when the Sku does not exist.
/// </summary>
public sealed class GetSkuByIdHandler
{
    private readonly ISkuRepository _skuRepository;
    private readonly IProductRepository _productRepository;

    public GetSkuByIdHandler(ISkuRepository skuRepository, IProductRepository productRepository)
    {
        _skuRepository = skuRepository;
        _productRepository = productRepository;
    }

    public async Task<SkuDetailsResponse?> HandleAsync(GetSkuByIdQuery query, CancellationToken cancellationToken)
    {
        var skuId = new SkuId(query.SkuId);

        var sku = await _skuRepository.GetByIdAsync(skuId, cancellationToken);

        if (sku is null)
        {
            return null;
        }

        var product = await _productRepository.GetByIdAsync(sku.ProductId, cancellationToken);
        var productStatus = product?.Status ?? ProductStatus.Draft;

        return new SkuDetailsResponse
        {
            SkuId = sku.Id.Value,
            ProductId = sku.ProductId.Value,
            Code = sku.Code.Value,
            Gtin = sku.Gtin,
            Status = sku.Status.ToString(),
            CommerciallyEligible = CommercialEligibility.CommercialEligibilityPolicy.IsEligible(productStatus, sku.Status),
            Attributes = sku.Attributes.Select(SkuAttributeResponseMapper.ToResponse).ToArray()
        };
    }
}
