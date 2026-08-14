namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

/// <summary>
/// Raised when a Google Taxonomy embeddings synchronization is requested
/// while another one is already running. The API layer translates this into
/// HTTP 409.
/// </summary>
public sealed class GoogleTaxonomyEmbeddingSynchronizationInProgressException : Exception
{
    public GoogleTaxonomyEmbeddingSynchronizationInProgressException()
        : base("A Google Product Taxonomy embedding synchronization is already running.")
    {
    }
}
