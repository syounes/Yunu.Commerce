namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;
/// <summary>
/// Read model for an active Catalog Segment Definition used as the source for
/// semantic embedding generation (docs task: "Implementar sincronização de
/// embeddings de segmentos"). Decoupled from the SQL Server row shape
/// (Catalog.SegmentDefinitions,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql,
/// deploy/databases/sqlserver/008-add-segment-assignment-scope.sql).
///
/// Intentionally distinct from
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentCatalog.SegmentDefinitionResponse"/>,
/// which serves transactional assignment resolution/API responses and does
/// not carry SemanticText or UpdatedAt.
/// </summary>
public sealed class SegmentDefinitionSource
{
    public required long SegmentDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? SemanticText { get; init; }

    public required string SelectionMode { get; init; }

    public required string AssignmentScope { get; init; }

    public required bool IsRequired { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
