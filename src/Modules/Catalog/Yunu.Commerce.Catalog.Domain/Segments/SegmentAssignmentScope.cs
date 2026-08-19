namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Where a Segment Definition may be assigned, mirroring the SQL Server
/// column Catalog.SegmentDefinitions.AssignmentScope
/// (deploy/databases/sqlserver/008-add-segment-assignment-scope.sql).
///
/// Product: only the Product may carry an assignment; Skus inherit the
/// Product's effective value.
/// Sku: only the Sku may carry an assignment; the Product must not.
/// ProductWithSkuOverride: the Product defines the base value; a Sku may
/// carry an explicit override that fully replaces it.
/// </summary>
public enum SegmentAssignmentScope
{
    Product,
    Sku,
    ProductWithSkuOverride
}
