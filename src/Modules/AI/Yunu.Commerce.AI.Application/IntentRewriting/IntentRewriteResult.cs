namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// Structured outcome of an Intent/Query Rewriter call (docs task:
/// "Intent/Query Rewriting"). Contains only textual hints; it never contains
/// official catalog identifiers (category ids, attribute/option ids, SKU ids).
/// Resolving hints to identifiers is performed later by SQL Server/pgvector
/// retrieval, not by this contract or its producer.
/// </summary>
/// <param name="CategoryHint">
/// Free-text category hint in the user's own words (e.g. "tênis para
/// corrida"), used for UI display and audit. May be ambiguous on its own; not
/// meant to be embedded directly for category retrieval.
/// </param>
/// <param name="CategorySearchQuery">
/// Short, disambiguated pt-BR category search query aligned with official
/// Google Product Taxonomy vocabulary (e.g. "sapatos esportivos para
/// corrida"), used as the primary text for the category embedding. Null when
/// no identifiable product/category applies. Never contains an official
/// GoogleCategoryId or asserts that a specific official category exists.
/// </param>
public sealed record IntentRewriteResult(
    string OriginalInput,
    string NormalizedQuery,
    string SemanticQuery,
    CatalogIntent Intent,
    string DetectedLanguage,
    string TargetLocale,
    string? CategoryHint,
    IReadOnlyList<AttributeHint> AttributeHints,
    IReadOnlyList<string> SearchTerms,
    decimal Confidence,
    string? CategorySearchQuery = null);
