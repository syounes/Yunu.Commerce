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

    public async Task<bool> ExistsBySegmentDefinitionIdAsync(
        Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document => document.SegmentAssignments != null
                && document.SegmentAssignments.Any(sa => sa.SegmentDefinitionId == segmentDefinitionId.Value))
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySegmentOptionIdAsync(
        Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId,
        CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document => document.SegmentAssignments != null
                && document.SegmentAssignments.Any(sa => sa.Options.Any(o => o.SegmentOptionId == segmentOptionId.Value)))
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> UpdateStatusAsync(
        SkuId id,
        SkuStatus expectedCurrentStatus,
        SkuStatus newStatus,
        CancellationToken cancellationToken)
    {
        var filter = Builders<SkuDocument>.Filter.Where(d =>
            d.Id == id.Value && d.Status == expectedCurrentStatus.ToString());

        var update = Builders<SkuDocument>.Update.Set(d => d.Status, newStatus.ToString());

        var result = await _collection.UpdateOneAsync(filter, update, options: null, cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> ExistsNonArchivedByProductIdAsync(ProductId productId, CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document => document.ProductId == productId.Value && document.Status != SkuStatus.Archived.ToString())
            .Limit(1)
            .AnyAsync(cancellationToken);
    }
}
