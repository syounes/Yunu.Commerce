namespace Yunu.Commerce.Catalog.Application.CategoryResolution;

/// <summary>
/// Configuration for Google category hint resolution, bound from
/// "AI:CategoryResolution" (docs task: "Google Category Resolution"). Values
/// are lab defaults, not universal rules; they are expected to be
/// recalibrated once real similarity scores are observed (logged by <see
/// cref="GoogleCategoryResolver"/>), mirroring the approach already taken for
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeResolutionOptions"/>.
/// </summary>
public sealed class CategoryResolutionOptions
{
    /// <summary>
    /// Logical AI model name (bound under "AI:Models") used to generate the
    /// query embedding. Must be the same model that generated the stored
    /// Google Taxonomy category embeddings; resolved explicitly via <see
    /// cref="Yunu.Commerce.AI.Application.Configuration.IAIModelCatalog"/>,
    /// never via the embedding orchestrator's default provider.
    /// </summary>
    public required string EmbeddingModel { get; init; }

    public required int TopK { get; init; }

    public required double MinimumSimilarity { get; init; }

    public required double MinimumScoreMargin { get; init; }

    public required bool IncludeCandidatesInResponse { get; init; }
}
