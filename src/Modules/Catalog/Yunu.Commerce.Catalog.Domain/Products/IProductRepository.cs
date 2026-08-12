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
}
