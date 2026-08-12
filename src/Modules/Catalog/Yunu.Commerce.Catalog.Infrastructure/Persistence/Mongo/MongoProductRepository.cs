using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB adapter implementing the Catalog.Domain IProductRepository port
/// (docs/adr/0003-database-per-bounded-context.md §9). Implements exactly the
/// existing contract; no Update/Delete/Search is added at this phase.
/// </summary>
public sealed class MongoProductRepository : IProductRepository
{
    internal const string CollectionName = "products";

    private readonly IMongoCollection<ProductDocument> _collection;

    public MongoProductRepository(IMongoClient mongoClient, IOptions<CatalogMongoOptions> options)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<ProductDocument>(CollectionName);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        var document = ProductDocumentMapper.ToDocument(product);

        await _collection.InsertOneAsync(document, options: null, cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ProductDocumentMapper.ToDomain(document);
    }
}
