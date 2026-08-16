namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Outcome of a contextual reranking call (docs task: "Contextual candidate
/// reranking"). Only ever produced after the provider's Structured Output
/// has been deserialized AND validated against the original candidate list
/// (docs restriction: "Nunca confie diretamente em IDs produzidos pelo
/// LLM"): <see cref="ICandidateReranker"/> implementations must never return
/// a <see cref="SelectedCandidateIndex"/> that does not exist in the request,
/// is negative, is duplicated in <see cref="Ranking"/>, or is inconsistent
/// with <see cref="Decision"/>. When those problems occur, the adapter throws
/// <see cref="CandidateRerankException"/> (a technical/provider failure)
/// instead of fabricating a result.
/// </summary>
public sealed record CandidateRerankResult(
    CandidateRerankDecision Decision,
    int? SelectedCandidateIndex,
    double Confidence,
    IReadOnlyList<RerankedCandidateScore> Ranking,
    string Reason);
