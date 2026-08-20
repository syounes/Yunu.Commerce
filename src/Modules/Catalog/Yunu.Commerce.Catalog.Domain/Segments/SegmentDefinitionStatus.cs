namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Lifecycle status of a Segment Definition, mirroring the SQL Server column
/// Catalog.SegmentDefinitions.Status. A newly created Definition always
/// starts as <see cref="Draft"/>; <see cref="Archived"/> is terminal.
/// </summary>
public enum SegmentDefinitionStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}
