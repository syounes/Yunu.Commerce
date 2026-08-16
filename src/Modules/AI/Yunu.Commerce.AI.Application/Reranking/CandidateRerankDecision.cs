namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Functional outcome of a candidate reranking call (docs task: "Contextual
/// candidate reranking"). This is a decision about the candidate list
/// provided, never a fabricated result: <see cref="Selected"/> always refers
/// to an index that existed in the request.
/// </summary>
public enum CandidateRerankDecision
{
    Selected,
    Ambiguous,
    None
}
