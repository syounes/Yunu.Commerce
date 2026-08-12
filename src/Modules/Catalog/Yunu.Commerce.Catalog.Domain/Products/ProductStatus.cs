namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Product lifecycle classification (docs/domains/catalog.md §19).
/// This phase implements the documented values only; transition rules
/// (Activate/Deactivate/Archive/SubmitForReview) are deferred until a
/// documented use case defines them.
/// </summary>
public enum ProductStatus
{
    Draft,
    PendingReview,
    Active,
    Inactive,
    Archived
}
