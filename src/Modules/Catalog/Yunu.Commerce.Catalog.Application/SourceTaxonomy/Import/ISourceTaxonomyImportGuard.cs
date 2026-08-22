namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Guards against two imports of the SAME SourceTaxonomy running
/// concurrently inside one application process
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §18). Guards are keyed
/// by SourceTaxonomyId so unrelated SourceTaxonomies can be imported
/// independently; this intentionally differs from
/// <c>IGoogleTaxonomySynchronizationGuard</c>, which serializes a single
/// process-wide Google synchronization.
/// </summary>
public interface ISourceTaxonomyImportGuard
{
    /// <summary>
    /// Attempts to acquire the import lock for the given SourceTaxonomyId.
    /// Returns an <see cref="IDisposable"/> release token when successful, or
    /// null when an import for that SourceTaxonomyId is already running.
    /// </summary>
    IDisposable? TryAcquire(long sourceTaxonomyId);
}
