namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Provider-neutral port for the generic
/// Integration.SourceTaxonomyImports lifecycle
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §8, §11). Kept
/// separate from <see cref="ISourceTaxonomyRepository"/> so the Phase 2
/// read/create repository does not become a synchronization kitchen sink.
///
/// The Started row must survive even when the synchronization transaction
/// that follows it fails; therefore <see cref="StartAsync"/>,
/// <see cref="MarkFailedAsync"/> commit independently of the synchronization
/// transaction, while <see cref="CompleteAsync"/> is expected to be invoked
/// by the SQL synchronization store from inside the same transaction that
/// applies the header/node changes (§12).
/// </summary>
public interface ISourceTaxonomyImportStore
{
    Task<long> StartAsync(
        long sourceTaxonomyId,
        string adapterCode,
        string? sourceUri,
        string? externalVersion,
        string? sourceChecksum,
        DateTime startedAtUtc,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        long importId,
        string errorMessage,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);
}
