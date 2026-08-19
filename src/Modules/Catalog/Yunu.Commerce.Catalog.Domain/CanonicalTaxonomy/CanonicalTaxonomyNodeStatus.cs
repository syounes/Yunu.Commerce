namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Lifecycle status of a Canonical Taxonomy node, mirroring the SQL Server
/// check constraint CK_CanonicalTaxonomyNodes_Status
/// (deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
/// </summary>
public enum CanonicalTaxonomyNodeStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}
