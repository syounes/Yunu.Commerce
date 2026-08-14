namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

/// <summary>
/// Guards against concurrent full Google Taxonomy embedding synchronizations
/// running at the same time. This is a distinct boundary from
/// <see cref="SynchronizeGoogleTaxonomy.IGoogleTaxonomySynchronizationGuard"/>:
/// that guard protects the SQL Server taxonomy import, while this one protects
/// the (potentially long-running, provider-calling) pgvector projection sync.
/// </summary>
public interface IGoogleTaxonomyEmbeddingSynchronizationGuard
{
    /// <summary>
    /// Attempts to acquire the synchronization lock. Returns an
    /// <see cref="IDisposable"/> release token when successful, or null when a
    /// synchronization is already running.
    /// </summary>
    IDisposable? TryAcquire();
}
