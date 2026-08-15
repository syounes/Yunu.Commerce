namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Configuration for semantic attribute hint resolution, bound from
/// "AI:AttributeResolution" (docs task: "Semantic attribute hint
/// resolution"). Values are lab defaults, not universal rules; they are
/// expected to be recalibrated once real similarity scores are observed
/// (logged by <see cref="AttributeHintResolver"/>).
/// </summary>
public sealed class AttributeResolutionOptions
{
    /// <summary>
    /// Logical AI model name (bound under "AI:Models") used to generate query
    /// embeddings. Must be the same model that generated the stored attribute
    /// embeddings; resolved explicitly, never via the embedding orchestrator's
    /// default provider, since multiple embedding models may be registered.
    /// </summary>
    public required string EmbeddingModel { get; init; }

    public required int TopK { get; init; }

    public required double DefinitionMinimumSimilarity { get; init; }

    public required double OptionMinimumSimilarity { get; init; }

    public required double MinimumScoreMargin { get; init; }

    public required bool IncludeCandidatesInResponse { get; init; }
}
