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
        var exists = _products.Values.Any(p => p.CanonicalTaxonomyNodeId == canonicalTaxonomyNodeId)
            || _canonicalTaxonomyNodeIdsInUse.Contains(canonicalTaxonomyNodeId);
        return Task.FromResult(exists);
    }

    private readonly HashSet<CanonicalTaxonomyNodeId> _canonicalTaxonomyNodeIdsInUse = new();

    /// <summary>
    /// Test-only helper to simulate a Canonical Taxonomy node being referenced
    /// by a Product, without requiring a fully constructed Product aggregate.
    /// </summary>
    public void MarkCanonicalTaxonomyNodeInUse(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId)
    {
        _canonicalTaxonomyNodeIdsInUse.Add(canonicalTaxonomyNodeId);
    }

    public Task<bool> ExistsByBrandIdAsync(BrandId brandId, CancellationToken cancellationToken)
    {
        var exists = _products.Values.Any(p => p.BrandId == brandId) || _brandIdsInUse.Contains(brandId);
        return Task.FromResult(exists);
    }

    private readonly HashSet<BrandId> _brandIdsInUse = new();

    /// <summary>
    /// Test-only helper to simulate a Brand being referenced by a Product,
    /// without requiring a fully constructed Product aggregate.
    /// </summary>
    public void MarkBrandInUse(BrandId brandId)
    {
        _brandIdsInUse.Add(brandId);
    }
}
