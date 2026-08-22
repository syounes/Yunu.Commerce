namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when a snapshot's ExternalTaxonomyId conflicts with the persisted
/// SourceTaxonomy's non-null ExternalTaxonomyId (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §8). Follows the same compatibility rule as ScopeCode: enrichment from
/// null is allowed, but a genuinely conflicting non-null value fails.
/// </summary>
public sealed class SourceTaxonomyExternalTaxonomyIdConflictException : Exception
{
    public SourceTaxonomyExternalTaxonomyIdConflictException(string existingExternalTaxonomyId, string snapshotExternalTaxonomyId)
        : base($"Snapshot ExternalTaxonomyId '{snapshotExternalTaxonomyId}' conflicts with the existing SourceTaxonomy ExternalTaxonomyId '{existingExternalTaxonomyId}'.")
    {
    }
}
