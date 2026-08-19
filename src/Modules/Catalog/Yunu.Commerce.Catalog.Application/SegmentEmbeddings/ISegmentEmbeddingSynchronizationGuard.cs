namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Guards against concurrent full Segment embedding synchronizations
/// running at the same time. Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.AttributeEmbeddings.IAttributeEmbeddingSynchronizationGuard"/>.
/// </summary>
public interface ISegmentEmbeddingSynchronizationGuard
{
    /// <summary>
    /// Attempts to acquire the synchronization lock. Returns an
    /// <see cref="IDisposable"/> release token when successful, or null when a
    /// synchronization is already running.
    /// </summary>
    IDisposable? TryAcquire();
}
