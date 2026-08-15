using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.Synchronization.InMemory;

/// <summary>
/// Process-local (single-instance) implementation of
/// <see cref="IAttributeEmbeddingSynchronizationGuard"/> using an in-memory
/// flag. Mirrors
/// <see cref="Catalog.Infrastructure.GoogleTaxonomy.Synchronization.InMemory.InMemoryGoogleTaxonomyEmbeddingSynchronizationGuard"/>.
/// Appropriate for the current local-development/Lab phase; a distributed
/// lock can replace this later without touching the Application use case.
/// </summary>
public sealed class InMemoryAttributeEmbeddingSynchronizationGuard : IAttributeEmbeddingSynchronizationGuard
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
        private readonly InMemoryAttributeEmbeddingSynchronizationGuard _guard;

        public ReleaseToken(InMemoryAttributeEmbeddingSynchronizationGuard guard)
        {
            _guard = guard;
        }

        public void Dispose()
        {
            Volatile.Write(ref _guard._isRunning, 0);
        }
    }
}
