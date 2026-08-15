namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// Structured outcome of an Intent/Query Rewriter call (docs task:
/// "Intent/Query Rewriting"). Contains only textual hints; it never contains
/// official catalog identifiers (category ids, attribute/option ids, SKU ids).
/// Resolving hints to identifiers is performed later by SQL Server/pgvector
/// retrieval, not by this contract or its producer.
/// </summary>
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
    decimal Confidence);
