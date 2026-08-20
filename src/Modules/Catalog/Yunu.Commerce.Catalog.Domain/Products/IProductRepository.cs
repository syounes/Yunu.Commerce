namespace Yunu.Commerce.Catalog.Domain.Products;

/// <summary>
/// Persistence port for the Product Aggregate Root (docs/domains/catalog.md §40-41,
/// docs/adr/0001-use-ddd-clean-hexagonal.md §9/§11).
///
/// This contract expresses only the persistence needs actually required by the first
/// Catalog use cases: adding a newly created Aggregate and reconstituting an existing
/// one by identity. It intentionally exposes no persistence-technology concept.
/// A future Infrastructure adapter (e.g. MongoProductRepository, per
/// docs/adr/0003-database-per-bounded-context.md §9) implements this port without
/// requiring any change to this interface or to the Product Aggregate.
///
/// Update/save coordination for mutations made to an already-loaded Product is
/// intentionally deferred until a concrete Application use case requires it.
/// </summary>
public interface IProductRepository
{
    Task AddAsync(
        Product product,
        CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(
        ProductId id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether any Product currently references the given Canonical Taxonomy
    /// node as its classification (docs task: "Canonical Taxonomy + Segments
    /// Domain" §22, §27). Used by Application to block UPDATE/DELETE of a
    /// node that is in use, and to validate a node before Product creation.
    /// </summary>
    Task<bool> ExistsByCanonicalTaxonomyNodeIdAsync(
        Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy.CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether any Product currently references the given Brand
    /// (docs task: "Canonical Taxonomy + Segments Domain" §36). Used by
    /// Application to block UPDATE/DELETE of a Brand that is in use.
    /// </summary>
    Task<bool> ExistsByBrandIdAsync(
        Brands.BrandId brandId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether any Product currently carries a Segment assignment for the
    /// given SegmentDefinition (docs task: "Yunu.Commerce V8 — Lifecycle +
    /// Usage Guards de Segments"). Used by Application to block archiving a
    /// SegmentDefinition that is still in use.
    /// </summary>
    Task<bool> ExistsBySegmentDefinitionIdAsync(
        Segments.SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether any Product currently selects the given SegmentOption within
    /// one of its Segment assignments (docs task: "Yunu.Commerce V8 —
    /// Lifecycle + Usage Guards de Segments"). Used by Application to block
    /// archiving a SegmentOption that is still in use.
    /// </summary>
    Task<bool> ExistsBySegmentOptionIdAsync(
        Segments.SegmentOptionId segmentOptionId,
        CancellationToken cancellationToken);
}
