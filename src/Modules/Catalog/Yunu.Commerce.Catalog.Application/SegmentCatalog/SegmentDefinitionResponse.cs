namespace Yunu.Commerce.Catalog.Application.SegmentCatalog;

/// <summary>
/// Dedicated read model for a Catalog Segment Definition, decoupled from the
/// SQL Server row shape (Catalog.SegmentDefinitions,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql,
/// deploy/databases/sqlserver/008-add-segment-assignment-scope.sql). Contains
/// only the fields required to resolve and validate a Segment assignment or
/// to serve a read-only Segments API (docs task: "Canonical Taxonomy +
/// Segments Domain" §24).
/// </summary>
public sealed class SegmentDefinitionResponse
{
    public required long SegmentDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? SemanticText { get; init; }

    public required string SelectionMode { get; init; }

    public required string AssignmentScope { get; init; }

    public required string Status { get; init; }
}
