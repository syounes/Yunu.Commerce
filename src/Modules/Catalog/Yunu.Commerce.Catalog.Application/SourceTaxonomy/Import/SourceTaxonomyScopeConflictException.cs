namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when a snapshot's ScopeCode conflicts with the persisted
/// SourceTaxonomy's non-null ScopeCode (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §8). A null snapshot ScopeCode never overwrites an existing non-null
/// value, and enrichment (existing null -> snapshot non-null) is allowed;
/// only genuinely conflicting non-null values fail.
/// </summary>
public sealed class SourceTaxonomyScopeConflictException : Exception
{
    public SourceTaxonomyScopeConflictException(string existingScopeCode, string snapshotScopeCode)
        : base($"Snapshot ScopeCode '{snapshotScopeCode}' conflicts with the existing SourceTaxonomy ScopeCode '{existingScopeCode}'.")
    {
    }
}
