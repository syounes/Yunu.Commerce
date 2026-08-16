namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Raised when an <see cref="ICandidateReranker"/> adapter fails to produce a
/// usable, schema-conformant, index-safe result (docs task: "Contextual
/// candidate reranking"). <see cref="Reason"/> lets callers distinguish
/// transient provider failures (timeout, rate limit, provider unavailable)
/// from content-filter/authentication/invalid-response failures, mirroring
/// <see cref="Yunu.Commerce.AI.Application.IntentRewriting.IntentRewriteException"/>.
/// Also raised when the provider's response references a candidate index
/// that does not exist, is negative, or is duplicated: such a response is
/// always treated as an invalid response from the provider, never as a
/// valid selection.
/// </summary>
public sealed class CandidateRerankException : Exception
{
    public CandidateRerankFailureReason Reason { get; }

    public CandidateRerankException(CandidateRerankFailureReason reason, string message) : base(message)
    {
        Reason = reason;
    }
}

/// <summary>
/// Classification of why an <see cref="ICandidateReranker"/> call failed
/// (docs task: "Contextual candidate reranking"), so callers can apply the
/// configured technical-failure fallback strategy without inspecting
/// provider-specific exception types.
/// </summary>
public enum CandidateRerankFailureReason
{
    Authentication,
    RateLimited,
    Timeout,
    ContentFiltered,
    InvalidResponse,
    ProviderUnavailable
}
