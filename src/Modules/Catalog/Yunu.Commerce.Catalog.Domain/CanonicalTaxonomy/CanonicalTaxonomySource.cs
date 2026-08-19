namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Origin of a Canonical Taxonomy node, mirroring the SQL Server check
/// constraint CK_CanonicalTaxonomyNodes_Source
/// (deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
/// </summary>
public enum CanonicalTaxonomySource
{
    Yunu,
    Google,
    Client
}
