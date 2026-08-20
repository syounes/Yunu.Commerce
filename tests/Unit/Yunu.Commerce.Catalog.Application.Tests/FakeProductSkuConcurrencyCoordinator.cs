using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only in-memory fake for IProductSkuConcurrencyCoordinator, backed by
/// the same FakeProductRepository/FakeSkuRepository instances used by a test
/// so both "sides" of the coordinator observe the same state. Exists
/// exclusively inside this test project.
/// </summary>
internal sealed class FakeProductSkuConcurrencyCoordinator : IProductSkuConcurrencyCoordinator
{
    private readonly FakeProductRepository _productRepository;
    private readonly FakeSkuRepository _skuRepository;

    public FakeProductSkuConcurrencyCoordinator(FakeProductRepository productRepository, FakeSkuRepository skuRepository)
    {
        _productRepository = productRepository;
        _skuRepository = skuRepository;
    }

    public async Task<ArchiveProductCoordinationResult> ArchiveProductAsync(
        ProductId productId,
        ProductStatus expectedCurrentStatus,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return ArchiveProductCoordinationResult.ProductNotFound;
        }

        if (product.Status != expectedCurrentStatus)
        {
            return ArchiveProductCoordinationResult.ConcurrencyConflict;
        }

        if (await _skuRepository.ExistsNonArchivedByProductIdAsync(productId, cancellationToken))
        {
            return ArchiveProductCoordinationResult.NonArchivedSkuExists;
        }

        var updated = await _productRepository.UpdateStatusAsync(productId, expectedCurrentStatus, ProductStatus.Archived, cancellationToken);

        return updated
            ? ArchiveProductCoordinationResult.Archived
            : ArchiveProductCoordinationResult.ConcurrencyConflict;
    }

    public async Task<CreateSkuCoordinationResult> CreateSkuIfProductNotArchivedAsync(
        Sku sku,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(sku.ProductId, cancellationToken);
        if (product is null)
        {
            return CreateSkuCoordinationResult.ProductNotFound;
        }

        if (product.Status == ProductStatus.Archived)
        {
            return CreateSkuCoordinationResult.ProductArchived;
        }

        await _skuRepository.AddAsync(sku, cancellationToken);
        return CreateSkuCoordinationResult.Created;
    }

    public async Task<SkuTransitionCoordinationResult> TransitionSkuIfProductNotArchivedAsync(
        SkuId skuId,
        SkuStatus expectedCurrentStatus,
        SkuStatus newStatus,
        CancellationToken cancellationToken)
    {
        var sku = await _skuRepository.GetByIdAsync(skuId, cancellationToken);
        if (sku is null)
        {
            return SkuTransitionCoordinationResult.SkuNotFound;
        }

        var product = await _productRepository.GetByIdAsync(sku.ProductId, cancellationToken);
        if (product is null)
        {
            return SkuTransitionCoordinationResult.ProductNotFound;
        }

        if (product.Status == ProductStatus.Archived)
        {
            return SkuTransitionCoordinationResult.ProductArchived;
        }

        var updated = await _skuRepository.UpdateStatusAsync(skuId, expectedCurrentStatus, newStatus, cancellationToken);

        return updated
            ? SkuTransitionCoordinationResult.Transitioned
            : SkuTransitionCoordinationResult.ConcurrencyConflict;
    }
}
