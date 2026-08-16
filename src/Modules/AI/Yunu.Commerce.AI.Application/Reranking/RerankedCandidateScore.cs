namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Relevance score assigned by the reranker to one candidate index (docs
/// task: "Contextual candidate reranking"). <see cref="CandidateIndex"/> is
/// validated by the caller against the original candidate list before any
/// use; it is never trusted blindly.
/// </summary>
public sealed record RerankedCandidateScore(
    int CandidateIndex,
    double RelevanceScore);
