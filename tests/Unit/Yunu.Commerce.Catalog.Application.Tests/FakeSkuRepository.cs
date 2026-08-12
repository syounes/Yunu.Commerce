using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Skus;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for ISkuRepository. Exists exclusively inside this test
/// project; no production InMemorySkuRepository is introduced at this phase.
/// </summary>
internal sealed class FakeSkuRepository : ISkuRepository
{
    private readonly Dictionary<Guid, Sku> _skus = new();

    public int AddAsyncCallCount { get; private set; }

    public Task AddAsync(Sku sku, CancellationToken cancellationToken)
    {
        AddAsyncCallCount++;
        _skus[sku.Id.Value] = sku;
        return Task.CompletedTask;
    }

    public Task<Sku?> GetByIdAsync(SkuId id, CancellationToken cancellationToken)
    {
        _skus.TryGetValue(id.Value, out var sku);
        return Task.FromResult(sku);
    }

    public Task<IReadOnlyCollection<Sku>> GetByProductIdAsync(ProductId productId, CancellationToken cancellationToken)
    {
        var result = _skus.Values
            .Where(sku => sku.ProductId == productId)
            .ToList();

        return Task.FromResult<IReadOnlyCollection<Sku>>(result);
    }
}
