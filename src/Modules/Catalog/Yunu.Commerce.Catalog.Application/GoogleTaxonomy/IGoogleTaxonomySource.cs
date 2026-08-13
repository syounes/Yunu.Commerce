namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Port abstracting retrieval of the raw Google Product Taxonomy text feed.
/// Infrastructure provides the HTTP-based implementation; Application never
/// depends on HttpClient or any Google-specific transport detail.
/// </summary>
public interface IGoogleTaxonomySource
{
    Task<IReadOnlyCollection<string>> GetTaxonomyAsync(CancellationToken cancellationToken);
}
