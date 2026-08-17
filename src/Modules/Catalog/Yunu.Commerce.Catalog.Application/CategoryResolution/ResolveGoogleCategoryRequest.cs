using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Input to <see cref="IGoogleCategoryResolver"/> (docs task: "Google Category
/// Resolution" + "Google Category reranking hardening"). <see
/// cref="RawCategoryHint"/> is the free-text category hint produced by the
/// Intent Rewriter (<see
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
/// <see cref="OriginalInput"/>, <see cref="NormalizedQuery"/> and <see
/// cref="AttributeHints"/> are optional, additional Intent Rewriter outputs
/// (docs task: "Google Category reranking hardening") forwarded to the
/// reranker only, so it can disambiguate polysemous terms (e.g. a shoe vs. a
/// sport) using gender/size/material facts already extracted by the Intent
/// Rewriter; they are never used for exact match or embedding retrieval and
/// callers that omit them (e.g. the isolated calibration endpoint) keep
/// working exactly as before.
/// </summary>
public sealed record ResolveGoogleCategoryRequest(
    string RawCategoryHint,
    string? SemanticQuery,
    string Locale = "pt-BR",
    string? CategorySearchQuery = null,
    string? OriginalInput = null,
    string? NormalizedQuery = null,
    IReadOnlyList<AttributeHint>? AttributeHints = null);
