using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
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

    public async Task<bool> ExistsByBrandIdAsync(BrandId brandId, CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document => document.BrandId == brandId.Value)
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCanonicalTaxonomyNodeIdAsync(CanonicalTaxonomyNodeId canonicalTaxonomyNodeId, CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document =>
                document.CanonicalTaxonomyNodeId == canonicalTaxonomyNodeId.Value)
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(ProductId id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ProductDocumentMapper.ToDomain(document);
    }

    public async Task<bool> ExistsBySegmentDefinitionIdAsync(
        Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document => document.SegmentAssignments.Any(sa => sa.SegmentDefinitionId == segmentDefinitionId.Value))
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySegmentOptionIdAsync(
        Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId segmentOptionId,
        CancellationToken cancellationToken)
    {
        return await _collection
            .Find(document => document.SegmentAssignments.Any(sa => sa.Options.Any(o => o.SegmentOptionId == segmentOptionId.Value)))
            .Limit(1)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> UpdateStatusAsync(
        ProductId id,
        ProductStatus expectedCurrentStatus,
        ProductStatus newStatus,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ProductDocument>.Filter.Where(d =>
            d.Id == id.Value && d.Status == expectedCurrentStatus.ToString());

        var update = Builders<ProductDocument>.Update.Set(d => d.Status, newStatus.ToString());

        var result = await _collection.UpdateOneAsync(filter, update, options: null, cancellationToken);

        return result.MatchedCount > 0;
    }
}
