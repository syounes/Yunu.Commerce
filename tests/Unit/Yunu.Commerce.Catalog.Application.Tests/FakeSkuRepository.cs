using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

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

    public Task<bool> ExistsBySegmentDefinitionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId, CancellationToken cancellationToken)
    {
        var exists = _skus.Values.Any(sku => sku.SegmentAssignments.Any(sa => sa.SegmentDefinitionId == segmentDefinitionId))
            || _segmentDefinitionIdsInUse.Contains(segmentDefinitionId);
        return Task.FromResult(exists);
    }

    private readonly HashSet<Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId> _segmentDefinitionIdsInUse = new();

    /// <summary>
    /// Test-only helper to simulate a SegmentDefinition being referenced by a
    /// Sku, without requiring a fully constructed Sku aggregate.
    /// </summary>
    public void MarkSegmentDefinitionInUse(Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId)
    {
        _segmentDefinitionIdsInUse.Add(segmentDefinitionId);
    }

    public Task<bool> ExistsBySegmentOptionIdAsync(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId, CancellationToken cancellationToken)
    {
        var exists = _skus.Values.Any(sku => sku.SegmentAssignments.Any(sa => sa.Options.Any(o => o.SegmentOptionId == segmentOptionId)))
            || _segmentOptionIdsInUse.Contains(segmentOptionId);
        return Task.FromResult(exists);
    }

    private readonly HashSet<Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId> _segmentOptionIdsInUse = new();

    /// <summary>
    /// Test-only helper to simulate a SegmentOption being referenced by a
    /// Sku, without requiring a fully constructed Sku aggregate.
    /// </summary>
    public void MarkSegmentOptionInUse(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId)
    {
        _segmentOptionIdsInUse.Add(segmentOptionId);
    }

    public Task<bool> UpdateStatusAsync(
        SkuId id,
        SkuStatus expectedCurrentStatus,
        SkuStatus newStatus,
        CancellationToken cancellationToken)
    {
        if (!_skus.TryGetValue(id.Value, out var sku) || sku.Status != expectedCurrentStatus)
        {
            return Task.FromResult(false);
        }

        switch (newStatus)
        {
            case SkuStatus.Active:
                sku.Activate();
                break;
            case SkuStatus.Inactive:
                sku.Block();
                break;
            case SkuStatus.Archived:
                sku.Discontinue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Unsupported Sku status transition.");
        }

        return Task.FromResult(true);
    }

    public Task<bool> ExistsNonArchivedByProductIdAsync(ProductId productId, CancellationToken cancellationToken)
    {
        var exists = _skus.Values.Any(sku => sku.ProductId == productId && sku.Status != SkuStatus.Archived);
        return Task.FromResult(exists);
    }
}
