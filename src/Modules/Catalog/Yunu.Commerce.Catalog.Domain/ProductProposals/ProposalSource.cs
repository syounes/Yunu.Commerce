namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Snapshot of the natural-language input and Intent Rewriter output that
/// originated a <see cref="ProductProposal"/> (docs task: "Catalog intent
/// resolution orchestration" - proposal persistence). Purely descriptive:
/// carries no official catalog identifiers.
/// </summary>
public sealed record ProposalSource(
    string OriginalInput,
    string NormalizedQuery,
    string SemanticQuery,
    string Intent,
    string DetectedLanguage,
    string TargetLocale);
