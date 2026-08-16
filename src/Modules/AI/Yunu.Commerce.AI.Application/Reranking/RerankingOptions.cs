namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Configuration for contextual candidate reranking, bound from
/// "AI:Reranking" (docs task: "Contextual candidate reranking"). Values are
/// lab defaults, mirroring the approach already taken for <see
/// cref="Yunu.Commerce.AI.Application.Configuration.AIOptions"/> and the
/// Catalog resolution options. Does not replace the existing vector
/// thresholds (Category/Attribute resolution options); those remain useful
/// as recall signals and as the technical-failure fallback path.
/// </summary>
public sealed class RerankingOptions
{
    /// <summary>
    /// Logical AI model name (bound under "AI:Models") used for reranking.
    /// Must be registered with ModelType = Chat; resolved explicitly via
    /// <see cref="Yunu.Commerce.AI.Application.Configuration.IAIModelCatalog"/>.
    /// </summary>
    public required string Model { get; init; }

    public required double MinimumConfidence { get; init; }

    public required double MinimumScoreMargin { get; init; }

    public required int MaximumCandidates { get; init; }

    public required bool AlwaysRerankSemanticMatches { get; init; }

    public required int MaxConcurrentRerankRequests { get; init; }

    /// <summary>
    /// Strategy applied when the reranker fails technically (timeout, rate
    /// limit, provider unavailable, invalid structured output, etc.), as
    /// opposed to a functional <see cref="CandidateRerankDecision"/>.
    /// </summary>
    public required TechnicalFailureFallbackStrategy TechnicalFailureFallback { get; init; }
}

/// <summary>
/// How a resolver should behave when the reranker fails technically (docs
/// task: "Contextual candidate reranking" §14). <see cref="VectorThreshold"/>
/// re-applies the pre-existing vector similarity threshold/margin decision to
/// the Top 1 candidate and marks the result as <c>VectorFallback</c>; it
/// never silently pretends reranking happened.
/// </summary>
public enum TechnicalFailureFallbackStrategy
{
    VectorThreshold
}
