using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Synchronization.InMemory;

/// <summary>
/// Process-local (single-instance) implementation of
/// <see cref="IGoogleTaxonomySynchronizationGuard"/> using an in-memory flag.
/// Appropriate for the current local-development phase; a distributed lock
/// (e.g. Redis) can replace this later without touching the Application use case.
/// </summary>
public sealed class InMemoryGoogleTaxonomySynchronizationGuard : IGoogleTaxonomySynchronizationGuard
{
    private int _isRunning;

    public IDisposable? TryAcquire()
    {
        return Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0
            ? new ReleaseToken(this)
            : null;
    }

    private sealed class ReleaseToken : IDisposable
    {
        private readonly InMemoryGoogleTaxonomySynchronizationGuard _guard;

        public ReleaseToken(InMemoryGoogleTaxonomySynchronizationGuard guard)
        {
            _guard = guard;
        }

        public void Dispose()
        {
            Volatile.Write(ref _guard._isRunning, 0);
        }
    }
}
