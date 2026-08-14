namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

/// <summary>
/// Provider-agnostic outcome returned after a Google Taxonomy category
/// embedding is generated and persisted. The full vector is intentionally
/// omitted; the AI module's smoke test endpoint already exposes it when
/// needed for inspection.
/// </summary>
public sealed class GenerateGoogleTaxonomyEmbeddingResult
{
    public required Guid Id { get; init; }

    public required int GoogleCategoryId { get; init; }

    public required string CategoryPath { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required int Dimensions { get; init; }
}
