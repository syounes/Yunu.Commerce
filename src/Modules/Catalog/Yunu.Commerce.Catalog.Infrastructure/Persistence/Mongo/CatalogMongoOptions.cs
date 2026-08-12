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
}
