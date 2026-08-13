using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Skus.CreateSku;

/// <summary>
/// Orchestrates creation of a new Sku Aggregate (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Business invariants are enforced entirely by Catalog.Domain (Sku.Create and its
/// Value Objects); this handler performs only mapping and persistence orchestration.
///
/// Product existence is validated before creating the Sku to prevent orphan SKUs.
/// This is an Application-level cross-Aggregate validation, not a Domain invariant
/// (the Sku Aggregate itself does not enforce Product existence).
/// </summary>
public sealed class CreateSkuHandler
{
    private readonly IProductRepository _productRepository;
    private readonly ISkuRepository _skuRepository;

    public CreateSkuHandler(IProductRepository productRepository, ISkuRepository skuRepository)
    {
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    public async Task<CreateSkuResult> HandleAsync(CreateSkuCommand command, CancellationToken cancellationToken)
    {
        var productId = new ProductId(command.ProductId);

        var productExists = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (productExists is null)
        {
            throw new InvalidOperationException($"Product with ID '{command.ProductId}' does not exist. Cannot create Sku for non-existent Product.");
        }

        var skuId = SkuId.New();
        var code = new SkuCode(command.Code);

        var sku = Sku.Create(skuId, productId, code, command.Gtin);

        await _skuRepository.AddAsync(sku, cancellationToken);

        return new CreateSkuResult
        {
            SkuId = skuId.Value
        };
    }
}
