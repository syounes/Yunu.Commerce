namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// HTTP response returned after a Google Taxonomy category embedding is
/// generated and persisted. The full vector is intentionally omitted; use
/// POST /api/ai/embeddings/google-category for embedding generation smoke tests.
/// </summary>
public sealed class GenerateGoogleTaxonomyEmbeddingResponse
{
    public required Guid Id { get; init; }

    public required int GoogleCategoryId { get; init; }

    public required string CategoryPath { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required int Dimensions { get; init; }
}
