namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Input to <see cref="IGoogleCategoryResolver"/> (docs task: "Google Category
/// Resolution"). <see cref="RawCategoryHint"/> is the free-text category hint
/// produced by the Intent Rewriter (<see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteResult.CategoryHint"/>),
/// used for UI/audit display and as a compatibility fallback. <see
/// cref="CategorySearchQuery"/> is the short, disambiguated, pt-BR category
/// search query (<see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteResult.CategorySearchQuery"/>)
/// and, when present, is the text actually used both for exact match and for
/// the embedding query; <see cref="RawCategoryHint"/> is only used when
/// <see cref="CategorySearchQuery"/> is null/blank (compatibility fallback for
/// older callers). <see cref="SemanticQuery"/> supplies additional product
/// context used only as reranker context, never concatenated into the
/// embedding text and never interpreted as a category name by itself.
/// </summary>
public sealed record ResolveGoogleCategoryRequest(
    string RawCategoryHint,
    string? SemanticQuery,
    string Locale = "pt-BR",
    string? CategorySearchQuery = null);
