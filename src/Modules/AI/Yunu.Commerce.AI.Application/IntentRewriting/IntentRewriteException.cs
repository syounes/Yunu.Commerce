namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// Raised when an <see cref="IIntentRewriter"/> adapter fails to produce a
/// usable, schema-conformant result (docs task: "Intent/Query Rewriting").
/// <see cref="Reason"/> lets the API layer distinguish transient provider
/// failures from configuration/auth/content-filter failures without parsing
/// the message text.
/// </summary>
public sealed class IntentRewriteException : Exception
{
    public IntentRewriteFailureReason Reason { get; }

    public IntentRewriteException(IntentRewriteFailureReason reason, string message) : base(message)
    {
        Reason = reason;
    }
}

/// <summary>
/// Classification of why an <see cref="IIntentRewriter"/> call failed (docs
/// task: "Intent/Query Rewriting"), so the API boundary can map to an
/// appropriate HTTP status without inspecting provider-specific exception
/// types.
/// </summary>
public enum IntentRewriteFailureReason
{
    Authentication,
    RateLimited,
    Timeout,
    ContentFiltered,
    InvalidResponse,
    ProviderUnavailable,
    OutputTruncated
}
