using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

/// <summary>
/// Test-only fake for IAttributeEmbeddingSynchronizationGuard that allows
/// simulating an already-running synchronization.
/// </summary>
internal sealed class FakeAttributeEmbeddingSynchronizationGuard : IAttributeEmbeddingSynchronizationGuard
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
