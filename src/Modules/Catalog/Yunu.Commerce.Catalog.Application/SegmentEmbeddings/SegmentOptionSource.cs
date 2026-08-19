namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Read model for an active Catalog Segment Option whose owning Segment
/// Definition is also active, used as the source for semantic embedding
/// generation (docs task: "Implementar sincronização de embeddings de
/// segmentos"). Decoupled from the SQL Server row shape
/// (Catalog.SegmentOptions,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
///
/// AssignmentScope is copied from the parent SegmentDefinition, matching the
/// invariant enforced by the PostgreSQL projection.
/// </summary>
public sealed class SegmentOptionSource
{
    public required long SegmentOptionId { get; init; }

    public required long SegmentDefinitionId { get; init; }

    public required string SegmentCode { get; init; }

    public required string SegmentName { get; init; }

    public required string OptionCode { get; init; }

    public required string OptionName { get; init; }

    public string? OptionDescription { get; init; }

    public string? OptionSemanticText { get; init; }

    public required string AssignmentScope { get; init; }

    public required int DisplayOrder { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
