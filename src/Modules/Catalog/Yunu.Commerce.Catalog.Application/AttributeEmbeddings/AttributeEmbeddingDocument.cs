namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Application-level persistence model for one row of
/// public.sku_attribute_embeddings (docs task: "SKU attribute embedding
/// synchronization pipeline"). This is a technical/semantic search artifact,
/// not a Catalog Domain concept: it is intentionally not a Domain Entity,
/// Aggregate Root or Value Object, mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding.GoogleTaxonomyEmbedding"/>.
///
/// GoogleCategoryId and SkuId are always null for AttributeDefinition/AttributeOption
/// documents produced by this pipeline; per-SKU values are out of scope.
/// </summary>
public sealed class AttributeEmbeddingDocument
{
    public required Guid Id { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    public required string AttributeCode { get; init; }

    public string? OptionCode { get; init; }

    public long? GoogleCategoryId { get; init; }

    public Guid? SkuId { get; init; }

    public required string Locale { get; init; }

    public required string Name { get; init; }

    public required string SemanticText { get; init; }

    public float[]? Embedding { get; init; }

    public string? EmbeddingModel { get; init; }

    public required string ContentHash { get; init; }

    public string? EmbeddedContentHash { get; init; }

    public required string Metadata { get; init; }

    public DateTime? SourceUpdatedAt { get; init; }

    public DateTime? EmbeddedAt { get; init; }

    public required bool IsActive { get; init; }
}
