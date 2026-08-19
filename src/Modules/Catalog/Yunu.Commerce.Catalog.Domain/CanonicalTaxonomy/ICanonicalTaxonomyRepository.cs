namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Persistence port for the CanonicalTaxonomyNode Aggregate Root (docs task:
/// "Canonical Taxonomy + Segments Domain" §4, §19-§22). Backed by SQL Server
/// (Catalog.CanonicalTaxonomyNodes,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql),
/// which remains the source of truth. Catalog.Domain never queries SQL
/// Server directly; Infrastructure implements this port with plain ADO.NET,
/// mirroring the existing GoogleTaxonomy/AttributeCatalog SQL Server adapters.
/// </summary>
public interface ICanonicalTaxonomyRepository
{
    /// <summary>
    /// Persists a new node. Returns the SQL Server IDENTITY-assigned id,
    /// since <see cref="CanonicalTaxonomyNode"/> is constructed by
    /// Application before the real id is known (docs task: "Canonical
    /// Taxonomy + Segments Domain" §19-§21).
    /// </summary>
    Task<CanonicalTaxonomyNodeId> AddAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken);

    Task UpdateAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken);

    Task DeleteAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken);

    Task<CanonicalTaxonomyNode?> GetByIdAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetChildrenAsync(CanonicalTaxonomyNodeId parentId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the given node currently has any children. Used by
    /// Catalog.Application to derive leaf-ness before allowing Update/Delete
    /// (docs task §22).
    /// </summary>
    Task<bool> HasChildrenAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken);
}
