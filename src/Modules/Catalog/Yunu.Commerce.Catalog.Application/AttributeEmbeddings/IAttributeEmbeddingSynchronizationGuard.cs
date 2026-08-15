namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Guards against concurrent full SKU attribute embedding synchronizations
/// running at the same time. Mirrors
/// <see cref="Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomyEmbeddings.IGoogleTaxonomyEmbeddingSynchronizationGuard"/>.
/// </summary>
public interface IAttributeEmbeddingSynchronizationGuard
{
    /// <summary>
    /// Attempts to acquire the synchronization lock. Returns an
    /// <see cref="IDisposable"/> release token when successful, or null when a
    /// synchronization is already running.
    /// </summary>
    IDisposable? TryAcquire();
}
