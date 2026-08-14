namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// HTTP request to generate and persist a semantic embedding for a Google
/// Product Taxonomy category hierarchy. <see cref="Provider"/> is optional;
/// when omitted, the AI module's configured DefaultProvider is used.
/// </summary>
public sealed class GenerateGoogleTaxonomyEmbeddingRequest
{
    public required int GoogleCategoryId { get; init; }

    public required string CategoryPath { get; init; }

    public string? Provider { get; init; }
}
