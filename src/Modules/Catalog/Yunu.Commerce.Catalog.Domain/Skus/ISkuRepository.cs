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

    /// <summary>
    /// Whether any Sku currently carries a Segment assignment for the given
    /// SegmentDefinition (docs task: "Yunu.Commerce V8 - Lifecycle + Usage
    /// Guards de Segments"). Used by Application to block archiving a
    /// SegmentDefinition that is still in use.
    /// </summary>
    Task<bool> ExistsBySegmentDefinitionIdAsync(
        Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether any Sku currently selects the given SegmentOption within one
    /// of its Segment assignments (docs task: "Yunu.Commerce V8 -
    /// Lifecycle + Usage Guards de Segments"). Used by Application to block
    /// archiving a SegmentOption that is still in use.
    /// </summary>
    Task<bool> ExistsBySegmentOptionIdAsync(
        Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically updates this Sku's persisted Status, using
    /// <paramref name="expectedCurrentStatus"/> as an optimistic-concurrency
    /// guard against the currently stored value
    /// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
    /// Returns <c>false</c> when no document matched (either the Sku does not
    /// exist, or its persisted Status no longer equals
    /// <paramref name="expectedCurrentStatus"/>), signalling the caller to
    /// reload and retry.
    /// </summary>
    Task<bool> UpdateStatusAsync(
        SkuId id,
        SkuStatus expectedCurrentStatus,
        SkuStatus newStatus,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether any Sku for the given Product currently has a Status other
    /// than Archived (docs/adr/0012). Used by Catalog.Application to guard a
    /// Product Archive transition: a Product cannot be archived while it
    /// still has a non-Archived Sku.
    /// </summary>
    Task<bool> ExistsNonArchivedByProductIdAsync(
        ProductId productId,
        CancellationToken cancellationToken);
}
