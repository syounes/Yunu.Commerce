namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Whether a Segment Definition accepts a single or multiple selected
/// options, mirroring the SQL Server check constraint
/// CK_SegmentDefinitions_SelectionMode
/// (deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
/// </summary>
public enum SegmentSelectionMode
{
    Single,
    Multiple
}
