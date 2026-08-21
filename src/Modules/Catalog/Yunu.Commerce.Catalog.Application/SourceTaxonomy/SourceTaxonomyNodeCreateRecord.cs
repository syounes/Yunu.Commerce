namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy;

/// <summary>
/// Provider-neutral input required to internally create one
/// Catalog.SourceTaxonomyNodes row (docs/adr/0014-provider-neutral-source-taxonomy.md).
/// SourceTaxonomyNodeId and CreatedAt are database-generated and therefore
/// not supplied here. UpdatedAt is intentionally absent: it remains null on
/// initial creation. ParentSourceTaxonomyNodeId is null for root nodes;
/// multiple roots per SourceTaxonomy are supported by design.
/// </summary>
public sealed record SourceTaxonomyNodeCreateRecord
{
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
    public required DateTime ImportedAt { get; init; }
}
