namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Result returned by <see cref="SourceTaxonomyImportOrchestrator"/> after a
/// successful import (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §9, §11).
/// </summary>
public sealed record SourceTaxonomyImportResult
{
    public required long ImportId { get; init; }
    public required long SourceTaxonomyId { get; init; }
    public required string AdapterCode { get; init; }
    public required int NodeCount { get; init; }
    public required int InsertedCount { get; init; }
    public required int UpdatedCount { get; init; }
    public required int DeactivatedCount { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
}
