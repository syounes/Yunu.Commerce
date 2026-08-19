namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Origin of a Canonical Taxonomy node, mirroring the SQL Server check
/// constraint CK_CanonicalTaxonomyNodes_Source
/// (deploy/databases/sqlserver/009-reset-canonical-taxonomy-starter.sql).
/// </summary>
public enum CanonicalTaxonomySource
{
    Yunu,
    Google,
    AI
}
