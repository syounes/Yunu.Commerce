namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Configuration for the Google Product Taxonomy import/synchronization feature.
/// Bound from the "Catalog:GoogleTaxonomy" configuration section. Kept in
/// Application (not Infrastructure) because both the HTTP source adapter and
/// the synchronization use case need the source URL/language, and neither
/// should hardcode it.
/// </summary>
public sealed class GoogleTaxonomyOptions
{
    public required string SourceUrl { get; init; }

    public required string Language { get; init; }
}
