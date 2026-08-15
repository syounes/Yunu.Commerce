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

    public GetSkuByIdHandler(ISkuRepository skuRepository)
    {
        _skuRepository = skuRepository;
    }

    public async Task<SkuDetailsResponse?> HandleAsync(GetSkuByIdQuery query, CancellationToken cancellationToken)
    {
        var skuId = new SkuId(query.SkuId);

        var sku = await _skuRepository.GetByIdAsync(skuId, cancellationToken);

        if (sku is null)
        {
            return null;
        }

        return new SkuDetailsResponse
        {
            SkuId = sku.Id.Value,
            ProductId = sku.ProductId.Value,
            Code = sku.Code.Value,
            Gtin = sku.Gtin,
            Status = sku.Status.ToString(),
            Attributes = sku.Attributes.Select(SkuAttributeResponseMapper.ToResponse).ToArray()
        };
    }
}
