using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for IProductRepository. Exists exclusively inside this test
/// project; no production InMemoryProductRepository is introduced at this phase.
/// </summary>
internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();

    public int AddAsyncCallCount { get; private set; }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        AddAsyncCallCount++;
        _products[product.Id.Value] = product;
        return Task.CompletedTask;
    }

    public Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        _products.TryGetValue(id.Value, out var product);
        return Task.FromResult(product);
    }

    public Task<bool> ExistsByCanonicalTaxonomyNodeIdAsync(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId, CancellationToken cancellationToken)
    {
        var exists = _products.Values.Any(p => p.CanonicalTaxonomyNodeId == canonicalTaxonomyNodeId);
        return Task.FromResult(exists);
    }

    public Task<bool> ExistsByBrandIdAsync(BrandId brandId, CancellationToken cancellationToken)
    {
        var exists = _products.Values.Any(p => p.BrandId == brandId);
        return Task.FromResult(exists);
    }
}
