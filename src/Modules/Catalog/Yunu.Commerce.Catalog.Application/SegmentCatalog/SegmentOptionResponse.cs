namespace Yunu.Commerce.Catalog.Application.SegmentCatalog;

/// <summary>
/// Dedicated read model for a Catalog Segment Option, decoupled from the SQL
/// Server row shape (Catalog.SegmentOptions,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
/// </summary>
public sealed class SegmentOptionResponse
{
    public required long SegmentOptionId { get; init; }

    public required long SegmentDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required int DisplayOrder { get; init; }

    public required string Status { get; init; }
}
