namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

/// <summary>
/// Guards against concurrent Google Taxonomy synchronizations running at the
/// same time. The current implementation is process-local, appropriate for a
/// single-instance local/dev environment; it is defined as an abstraction so a
/// distributed lock (e.g. Redis) can replace it later without touching the
/// use case (docs/adr/0006-use-redis-for-distributed-cache.md).
/// </summary>
public interface IGoogleTaxonomySynchronizationGuard
{
    /// <summary>
    /// Attempts to acquire the synchronization lock. Returns an
    /// <see cref="IDisposable"/> release token when successful, or null when a
    /// synchronization is already running.
    /// </summary>
    IDisposable? TryAcquire();
}
