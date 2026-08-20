namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Lifecycle status of a Segment Option, mirroring the SQL Server column
/// Catalog.SegmentOptions.Status
/// (deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
/// A newly created Option always starts as <see cref="Draft"/>;
/// <see cref="Archived"/> is terminal. Same shape as
/// <see cref="SegmentDefinitionStatus"/>, but kept as an independent enum:
/// a SegmentOption's lifecycle is not coupled to its parent
/// SegmentDefinition's lifecycle (docs task: "Implementar Domain +
/// Write-Side de SegmentOption").
/// </summary>
public enum SegmentOptionStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}
