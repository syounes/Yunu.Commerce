namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

/// <summary>
/// Application-level persistence model for a Google Taxonomy category
/// embedding. This is a technical/semantic search artifact, not a Catalog
/// Domain concept: it is intentionally not a Domain Entity, Aggregate Root or
/// Value Object (docs §11, embeddings support search, they do not belong to
/// the Product/Sku aggregate boundary).
/// </summary>
public sealed class GoogleTaxonomyEmbedding
{
    public required Guid Id { get; init; }

    public required int GoogleCategoryId { get; init; }

    public required string CategoryPath { get; init; }

    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required int Dimensions { get; init; }

    public required float[] Embedding { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
