namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Input to <see cref="IGoogleCategoryResolver"/> (docs task: "Google Category
/// Resolution"). <see cref="RawCategoryHint"/> is the free-text category hint
/// produced by the Intent Rewriter (<see
/// cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteResult.CategoryHint"/>);
/// <see cref="SemanticQuery"/> supplies additional product context
/// (<see cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteResult.SemanticQuery"/>)
/// used only to compose the embedding query text, never persisted or
/// interpreted as a category name by itself.
/// </summary>
public sealed record ResolveGoogleCategoryRequest(
    string RawCategoryHint,
    string? SemanticQuery,
    string Locale = "pt-BR");
