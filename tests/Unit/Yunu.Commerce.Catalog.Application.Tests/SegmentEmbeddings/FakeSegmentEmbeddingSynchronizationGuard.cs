using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentEmbeddings;

/// <summary>
/// Test-only fake for ISegmentEmbeddingSynchronizationGuard that allows
/// simulating an already-running synchronization.
/// </summary>
internal sealed class FakeSegmentEmbeddingSynchronizationGuard : ISegmentEmbeddingSynchronizationGuard
{
    public bool AlwaysBusy { get; set; }

    public int TryAcquireCallCount { get; private set; }

    public IDisposable? TryAcquire()
    {
        TryAcquireCallCount++;
        return AlwaysBusy ? null : new NoopReleaseToken();
    }

    private sealed class NoopReleaseToken : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
