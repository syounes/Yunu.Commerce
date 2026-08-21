namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy;

/// <summary>
/// Provider-neutral read model for a persisted SourceTaxonomyNode
/// (docs/adr/0014-provider-neutral-source-taxonomy.md).
/// Mirrors the columns of Catalog.SourceTaxonomyNodes
/// (deploy/databases/sqlserver/014-create-source-taxonomy-foundation.sql).
/// ExternalNodeId, NodeType and ParentSourceTaxonomyNodeId remain
/// intentionally open-ended/provider-neutral: no provider-specific column
/// or enum is exposed here.
/// </summary>
public sealed record SourceTaxonomyNodeRecord
{
    public required long SourceTaxonomyNodeId { get; init; }
    public required long SourceTaxonomyId { get; init; }
    public required string ExternalNodeId { get; init; }
    public long? ParentSourceTaxonomyNodeId { get; init; }
    public required string NodeType { get; init; }
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required int Level { get; init; }
    public required bool IsLeaf { get; init; }
    public required bool IsActive { get; init; }
    public required string SourceLanguage { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required DateTime ImportedAt { get; init; }
}
