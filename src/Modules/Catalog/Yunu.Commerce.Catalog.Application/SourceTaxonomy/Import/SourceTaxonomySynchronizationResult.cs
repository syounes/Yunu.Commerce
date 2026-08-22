namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Generic, provider-neutral synchronization outcome
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §8, §15, §16). Counts
/// are node-based, not SQL-statement-based; a node with several changed
/// fields counts once as Updated, and a deactivated node is never
/// double-counted as Updated.
/// </summary>
public sealed record SourceTaxonomySynchronizationResult
{
    public required int NodeCount { get; init; }
    public required int InsertedCount { get; init; }
    public required int UpdatedCount { get; init; }
    public required int DeactivatedCount { get; init; }
    public required bool WasSkippedByChecksum { get; init; }
}
