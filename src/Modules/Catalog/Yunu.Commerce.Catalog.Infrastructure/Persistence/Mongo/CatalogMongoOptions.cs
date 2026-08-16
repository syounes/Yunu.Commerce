namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// Environment-specific MongoDB configuration for Catalog persistence
/// (docs/adr/0003-database-per-bounded-context.md §9). Contains only values that
/// genuinely vary by environment; the collection name is an internal constant
/// for this phase (see <see cref="MongoProductRepository"/>).
/// </summary>
public sealed class CatalogMongoOptions
{
    public required string ConnectionString { get; init; }

    public required string DatabaseName { get; init; }

    /// <summary>
    /// Collection name used to persist <see
    /// cref="Yunu.Commerce.Catalog.Domain.ProductProposals.ProductProposal"/>
    /// (docs task: "Catalog intent resolution orchestration" - proposal
    /// persistence). Configurable (unlike the Product/Sku collection names,
    /// which remain internal constants for this phase) because it is a new
    /// collection introduced independently of environment-specific defaults.
    /// </summary>
    public string ProductProposalsCollectionName { get; init; } = "catalog_product_proposals";
}
