using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentEmbeddings.Synchronization.InMemory;

/// <summary>
/// Process-local (single-instance) implementation of
/// <see cref="ISegmentEmbeddingSynchronizationGuard"/> using an in-memory
/// flag. Mirrors
/// <see cref="Catalog.Infrastructure.AttributeEmbeddings.Synchronization.InMemory.InMemoryAttributeEmbeddingSynchronizationGuard"/>.
/// Appropriate for the current local-development/Lab phase; a distributed
/// lock can replace this later without touching the Application use case.
/// </summary>
public sealed class InMemorySegmentEmbeddingSynchronizationGuard : ISegmentEmbeddingSynchronizationGuard
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
        private readonly InMemorySegmentEmbeddingSynchronizationGuard _guard;

        public ReleaseToken(InMemorySegmentEmbeddingSynchronizationGuard guard)
        {
            _guard = guard;
        }

        public void Dispose()
        {
            Volatile.Write(ref _guard._isRunning, 0);
        }
    }
}
