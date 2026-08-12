using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Orchestrates creation of a new Sku Aggregate (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Business invariants are enforced entirely by Catalog.Domain (Sku.Create and its
/// Value Objects); this handler performs only mapping and persistence orchestration.
///
/// Existence of the referenced Product is not validated here: cross-Aggregate
/// existence checks are deferred until a documented use case requires them.
/// </summary>
public sealed class CreateSkuHandler
{
    private readonly ISkuRepository _skuRepository;

    public CreateSkuHandler(ISkuRepository skuRepository)
    {
        _skuRepository = skuRepository;
    }

    public async Task<CreateSkuResult> HandleAsync(CreateSkuCommand command, CancellationToken cancellationToken)
    {
        var skuId = SkuId.New();
        var productId = new ProductId(command.ProductId);
        var code = new SkuCode(command.Code);

        var sku = Sku.Create(skuId, productId, code, command.Gtin);

        await _skuRepository.AddAsync(sku, cancellationToken);

        return new CreateSkuResult
        {
            SkuId = skuId.Value
        };
    }
}
