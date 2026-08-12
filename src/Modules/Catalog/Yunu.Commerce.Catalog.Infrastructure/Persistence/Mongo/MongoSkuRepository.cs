using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB adapter implementing the Catalog.Domain ISkuRepository port
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md). Persists
/// Sku Aggregates in their own "skus" collection, independent from "products".
/// </summary>
public sealed class MongoSkuRepository : ISkuRepository
{
    internal const string CollectionName = "skus";

    private readonly IMongoCollection<SkuDocument> _collection;

    public MongoSkuRepository(IMongoClient mongoClient, IOptions<CatalogMongoOptions> options)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<SkuDocument>(CollectionName);
    }

    public async Task AddAsync(Sku sku, CancellationToken cancellationToken)
    {
        var document = SkuDocumentMapper.ToDocument(sku);

        await _collection.InsertOneAsync(document, options: null, cancellationToken);
    }

    public async Task<Sku?> GetByIdAsync(SkuId id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : SkuDocumentMapper.ToDomain(document);
    }

    public async Task<IReadOnlyCollection<Sku>> GetByProductIdAsync(ProductId productId, CancellationToken cancellationToken)
    {
        var documents = await _collection
            .Find(d => d.ProductId == productId.Value)
            .ToListAsync(cancellationToken);

        return documents.Select(SkuDocumentMapper.ToDomain).ToList();
    }
}
