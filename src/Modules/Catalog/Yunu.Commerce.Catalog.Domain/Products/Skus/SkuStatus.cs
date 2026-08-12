namespace Yunu.Commerce.Catalog.Domain.Products.Skus;

/// <summary>
/// Sku lifecycle classification (docs/domains/catalog.md §20).
/// This phase implements the documented values only; transition rules are
/// deferred until a documented use case defines them.
/// </summary>
public enum SkuStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}
