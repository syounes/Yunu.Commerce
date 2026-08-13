namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// HTTP response contract for a single ancestor entry returned by the
/// ancestors endpoint (root-to-leaf order).
/// </summary>
public sealed class GoogleTaxonomyAncestorResponse
{
    public required int GoogleCategoryId { get; init; }

    public required string Name { get; init; }

    public required int Level { get; init; }
}
