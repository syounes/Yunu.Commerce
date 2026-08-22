namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Provider-neutral port that applies one validated
/// <see cref="SourceTaxonomySnapshot"/> atomically against SQL Server
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §13-§17). A single
/// implementation performs, inside one transaction:
///
/// 1. checksum-based unchanged-snapshot short-circuit (§16);
/// 2. source identity safety checks (ProviderCode/ScopeCode/ExternalTaxonomyId, §8);
/// 3. two-pass node upsert/reactivation/deactivation (§14);
/// 4. header metadata refresh (§17);
/// 5. marking the pre-existing import row Completed (§12), so a Completed
///    history row can never survive a rolled-back synchronization.
///
/// No provider parsing logic belongs here; this store only understands the
/// normalized model.
/// </summary>
public interface ISourceTaxonomySynchronizationStore
{
    Task<SourceTaxonomySynchronizationResult> ApplyAsync(
        long sourceTaxonomyId,
        long importId,
        SourceTaxonomySnapshot snapshot,
        DateTime importedAtUtc,
        CancellationToken cancellationToken);
}
