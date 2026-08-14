namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

/// <summary>
/// Raised when a Google Taxonomy synchronization is requested while another
/// one is already running. The API layer translates this into HTTP 409.
/// </summary>
public sealed class GoogleTaxonomySynchronizationInProgressException : Exception
{
    public GoogleTaxonomySynchronizationInProgressException()
        : base("A Google Product Taxonomy synchronization is already running.")
    {
    }
}
