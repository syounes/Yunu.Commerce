namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// Catalog-facing intent classification produced by the Intent Rewriter
/// (docs task: "Intent/Query Rewriting"). Kept intentionally small for this
/// first version; extend rather than duplicate if additional bounded-context
/// use cases need catalog intent classification.
/// </summary>
public enum CatalogIntent
{
    CatalogSearch,
    ProductCreation,
    ProductUpdate,
    Unknown
}
