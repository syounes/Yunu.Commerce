using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings;

namespace Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Synchronization.InMemory;

/// <summary>
/// Process-local (single-instance) implementation of
/// <see cref="IGoogleTaxonomyEmbeddingSynchronizationGuard"/> using an
/// in-memory flag. Appropriate for the current local-development/Lab phase;
/// a distributed lock can replace this later without touching the
/// Application use case.
/// </summary>
public sealed class InMemoryGoogleTaxonomyEmbeddingSynchronizationGuard : IGoogleTaxonomyEmbeddingSynchronizationGuard
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
        private readonly InMemoryGoogleTaxonomyEmbeddingSynchronizationGuard _guard;

        public ReleaseToken(InMemoryGoogleTaxonomyEmbeddingSynchronizationGuard guard)
        {
            _guard = guard;
        }

        public void Dispose()
        {
            Volatile.Write(ref _guard._isRunning, 0);
        }
    }
}
