namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// One normalized node inside a <see cref="SourceTaxonomySnapshot"/>
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §5, §7).
/// <see cref="ParentExternalNodeId"/> exists ONLY in this in-memory/import
/// contract; it is never persisted as a second parent-identity column. The
/// generic synchronizer resolves it into
/// <c>ParentSourceTaxonomyNodeId</c> during the two-pass hierarchy
/// resolution (ADR-0014 §7, §14).
/// </summary>
public sealed record SourceTaxonomySnapshotNode
{
    public required string ExternalNodeId { get; init; }
    public string? ParentExternalNodeId { get; init; }
    public required string NodeType { get; init; }
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required int Level { get; init; }
    public required bool IsLeaf { get; init; }
    public required bool IsActive { get; init; }
}
