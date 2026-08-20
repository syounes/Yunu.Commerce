using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Domain.Concurrency;

/// <summary>
/// Result of an attempt to archive a Product coordinated with the Sku
/// Aggregate (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// </summary>
public enum ArchiveProductCoordinationResult
{
    Archived,
    ProductNotFound,
    ConcurrencyConflict,
    NonArchivedSkuExists
}

/// <summary>
/// Result of an attempt to create a Sku coordinated with its owning
/// Product's Archive status (docs/adr/0012).
/// </summary>
public enum CreateSkuCoordinationResult
{
    Created,
    ProductNotFound,
    ProductArchived
}

/// <summary>
/// Result of an attempt to transition a Sku to a non-Archived Status (i.e.
/// activate or block it), coordinated with its owning Product's Archive
/// status (docs/adr/0012).
/// </summary>
public enum SkuTransitionCoordinationResult
{
    Transitioned,
    SkuNotFound,
    ProductNotFound,
    ProductArchived,
    ConcurrencyConflict
}

/// <summary>
/// Small cross-aggregate coordination port
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
///
/// Product and Sku remain independent Aggregate Roots, each with its own
/// repository; this port exists exclusively to atomically enforce the single
/// invariant that spans both of them:
///
///     Product.Status == Archived  =&gt;  no Sku of that Product has a
///     Status other than Archived.
///
/// Without this coordination, "read Skus, then write Product" (Archive) and
/// "read Product, then write Sku" (CreateSku/reactivate/block) can each pass
/// their own guard check against a state that changes before the other
/// operation commits (write skew). Implementations must serialize the
/// operations covered by this port against the same underlying Product
/// document so at most one of two concurrently racing operations can
/// succeed, never both.
///
/// This is not a general-purpose distributed lock: it only covers the three
/// operations that can violate the invariant above.
/// </summary>
public interface IProductSkuConcurrencyCoordinator
{
    Task<ArchiveProductCoordinationResult> ArchiveProductAsync(
        ProductId productId,
        ProductStatus expectedCurrentStatus,
        CancellationToken cancellationToken);

    Task<CreateSkuCoordinationResult> CreateSkuIfProductNotArchivedAsync(
        Sku sku,
        CancellationToken cancellationToken);

    Task<SkuTransitionCoordinationResult> TransitionSkuIfProductNotArchivedAsync(
        SkuId skuId,
        SkuStatus expectedCurrentStatus,
        SkuStatus newStatus,
        CancellationToken cancellationToken);
}
