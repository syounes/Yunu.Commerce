using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Domain.Skus;

/// <summary>
/// Persistence port for the Sku Aggregate Root (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Mirrors the minimal shape of <see cref="IProductRepository"/>: only the
/// persistence operations actually required by current use cases are exposed.
/// No MongoDB-specific or otherwise vendor-specific type is exposed here.
/// </summary>
public interface ISkuRepository
{
    Task AddAsync(
        Sku sku,
        CancellationToken cancellationToken);

    Task<Sku?> GetByIdAsync(
        SkuId id,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Sku>> GetByProductIdAsync(
        ProductId productId,
        CancellationToken cancellationToken);
}
