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
        _products[product.Id.Value] = Snapshot(product);
        return Task.CompletedTask;
    }

    public Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        _products.TryGetValue(id.Value, out var product);
        return Task.FromResult(product is null ? null : Snapshot(product));
    }

    /// <summary>
    /// Returns an independent copy of the given Product, matching real
    /// persistence semantics (each GetByIdAsync/AddAsync call yields a
    /// distinct Aggregate instance, never a shared in-memory reference).
    /// This is required for tests to accurately observe concurrency
    /// conflicts: mutating one loaded instance must never be visible through
    /// another instance loaded before the persisted write actually happened.
    /// </summary>
    private static Product Snapshot(Product product)
    {
        return Product.Hydrate(
            product.Id,
            product.Name,
            product.Description,
            product.BrandId,
            product.CanonicalTaxonomyNodeId,
            product.Status,
            product.SegmentAssignments);
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

    public Task<bool> ExistsBySegmentDefinitionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId, CancellationToken cancellationToken)
    {
        var exists = _products.Values.Any(p => p.SegmentAssignments.Any(sa => sa.SegmentDefinitionId == segmentDefinitionId))
            || _segmentDefinitionIdsInUse.Contains(segmentDefinitionId);
        return Task.FromResult(exists);
    }

    private readonly HashSet<Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId> _segmentDefinitionIdsInUse = new();

    /// <summary>
    /// Test-only helper to simulate a SegmentDefinition being referenced by a
    /// Product, without requiring a fully constructed Product aggregate.
    /// </summary>
    public void MarkSegmentDefinitionInUse(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId)
    {
        _segmentDefinitionIdsInUse.Add(segmentDefinitionId);
    }

    public Task<bool> ExistsBySegmentOptionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId, CancellationToken cancellationToken)
    {
        var exists = _products.Values.Any(p => p.SegmentAssignments.Any(sa => sa.Options.Any(o => o.SegmentOptionId == segmentOptionId)))
            || _segmentOptionIdsInUse.Contains(segmentOptionId);
        return Task.FromResult(exists);
    }

    private readonly HashSet<Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId> _segmentOptionIdsInUse = new();

    /// <summary>
    /// Test-only helper to simulate a SegmentOption being referenced by a
    /// Product, without requiring a fully constructed Product aggregate.
    /// </summary>
    public void MarkSegmentOptionInUse(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId)
    {
        _segmentOptionIdsInUse.Add(segmentOptionId);
    }

    public Task<bool> UpdateStatusAsync(
        ProductId id,
        ProductStatus expectedCurrentStatus,
        ProductStatus newStatus,
        CancellationToken cancellationToken)
    {
        if (!_products.TryGetValue(id.Value, out var product) || product.Status != expectedCurrentStatus)
        {
            return Task.FromResult(false);
        }

        product.TransitionTo(newStatus);
        return Task.FromResult(true);
    }
}
