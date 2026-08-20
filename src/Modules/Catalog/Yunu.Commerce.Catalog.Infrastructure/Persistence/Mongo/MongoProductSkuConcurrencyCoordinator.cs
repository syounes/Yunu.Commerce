using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.Concurrency;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB adapter for <see cref="IProductSkuConcurrencyCoordinator"/>
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
///
/// Product and Sku remain independent Aggregates persisted in their own
/// "products"/"skus" collections; no Aggregate boundary is changed. This
/// coordinator only adds an atomic, transactional coordination step for the
/// three operations that can otherwise write-skew the cross-aggregate
/// invariant "Product Archived ⇒ no non-Archived Sku":
///
/// - ArchiveProductAsync: inside a single MongoDB transaction, atomically
///   increments the Product's LifecycleRevision (optimistic-concurrency
///   guard against a concurrent Status change) and, still inside the same
///   transaction, re-checks for a non-Archived Sku before writing
///   Status = Archived. Any concurrent CreateSku/reactivate/block that
///   commits its own LifecycleRevision bump first causes this transaction to
///   lose the race and abort (via the driver's automatic transient-
///   transaction-error retry only for the underlying MongoDB "WriteConflict"
///   case; a genuine loss surfaces as a normal coordination result instead of
///   an unexpected exception).
/// - CreateSkuIfProductNotArchivedAsync / TransitionSkuIfProductNotArchivedAsync:
///   inside a single MongoDB transaction, atomically bump the same Product's
///   LifecycleRevision (conditioned on the Product not being Archived) before
///   writing the Sku document. Because both "sides" of the race must bump the
///   very same field on the very same Product document before writing their
///   own Aggregate, MongoDB guarantees only one of two concurrently racing
///   transactions can commit; the loser is retried by the driver against the
///   now-current state and, if the Product has since become Archived,
///   correctly fails with ProductArchived instead of silently interleaving.
///
/// This requires MongoDB configured as a replica set (even a single-node one
/// for local/dev/test) because standalone MongoDB does not support
/// multi-document transactions (docs task: "V11 - Product/Sku Lifecycle
/// Concurrency"; see deploy/docker/docker-compose.yml).
/// </summary>
public sealed class MongoProductSkuConcurrencyCoordinator : IProductSkuConcurrencyCoordinator
{
    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<ProductDocument> _products;
    private readonly IMongoCollection<SkuDocument> _skus;

    public MongoProductSkuConcurrencyCoordinator(IMongoClient mongoClient, IOptions<CatalogMongoOptions> options)
    {
        _mongoClient = mongoClient;
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _products = database.GetCollection<ProductDocument>(MongoProductRepository.CollectionName);
        _skus = database.GetCollection<SkuDocument>(MongoSkuRepository.CollectionName);
    }

    public async Task<ArchiveProductCoordinationResult> ArchiveProductAsync(
        ProductId productId,
        ProductStatus expectedCurrentStatus,
        CancellationToken cancellationToken)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);

        return await session.WithTransactionAsync(async (s, ct) =>
        {
            var product = await _products.Find(s, d => d.Id == productId.Value).FirstOrDefaultAsync(ct);
            if (product is null)
            {
                return ArchiveProductCoordinationResult.ProductNotFound;
            }

            if (product.Status != expectedCurrentStatus.ToString())
            {
                return ArchiveProductCoordinationResult.ConcurrencyConflict;
            }

            var hasNonArchivedSku = await _skus
                .Find(s, d => d.ProductId == productId.Value && d.Status != SkuStatus.Archived.ToString())
                .Limit(1)
                .AnyAsync(ct);

            if (hasNonArchivedSku)
            {
                return ArchiveProductCoordinationResult.NonArchivedSkuExists;
            }

            var filter = Builders<ProductDocument>.Filter.Where(d =>
                d.Id == productId.Value
                && d.Status == expectedCurrentStatus.ToString()
                && d.LifecycleRevision == product.LifecycleRevision);

            var update = Builders<ProductDocument>.Update
                .Set(d => d.Status, ProductStatus.Archived.ToString())
                .Inc(d => d.LifecycleRevision, 1);

            var result = await _products.UpdateOneAsync(s, filter, update, options: null, ct);

            return result.MatchedCount > 0
                ? ArchiveProductCoordinationResult.Archived
                : ArchiveProductCoordinationResult.ConcurrencyConflict;
        }, cancellationToken: cancellationToken);
    }

    public async Task<CreateSkuCoordinationResult> CreateSkuIfProductNotArchivedAsync(
        Sku sku,
        CancellationToken cancellationToken)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);

        return await session.WithTransactionAsync(async (s, ct) =>
        {
            var product = await _products.Find(s, d => d.Id == sku.ProductId.Value).FirstOrDefaultAsync(ct);
            if (product is null)
            {
                return CreateSkuCoordinationResult.ProductNotFound;
            }

            if (product.Status == ProductStatus.Archived.ToString())
            {
                return CreateSkuCoordinationResult.ProductArchived;
            }

            var touchFilter = Builders<ProductDocument>.Filter.Where(d =>
                d.Id == sku.ProductId.Value
                && d.Status != ProductStatus.Archived.ToString()
                && d.LifecycleRevision == product.LifecycleRevision);

            var touchUpdate = Builders<ProductDocument>.Update.Inc(d => d.LifecycleRevision, 1);

            var touchResult = await _products.UpdateOneAsync(s, touchFilter, touchUpdate, options: null, ct);

            if (touchResult.MatchedCount == 0)
            {
                // Lost the race against a concurrent Archive: fail this
                // attempt instead of writing an orphaned/inconsistent Sku.
                return CreateSkuCoordinationResult.ProductArchived;
            }

            var document = SkuDocumentMapper.ToDocument(sku);
            await _skus.InsertOneAsync(s, document, options: null, ct);

            return CreateSkuCoordinationResult.Created;
        }, cancellationToken: cancellationToken);
    }

    public async Task<SkuTransitionCoordinationResult> TransitionSkuIfProductNotArchivedAsync(
        SkuId skuId,
        SkuStatus expectedCurrentStatus,
        SkuStatus newStatus,
        CancellationToken cancellationToken)
    {
        using var session = await _mongoClient.StartSessionAsync(cancellationToken: cancellationToken);

        return await session.WithTransactionAsync(async (s, ct) =>
        {
            var sku = await _skus.Find(s, d => d.Id == skuId.Value).FirstOrDefaultAsync(ct);
            if (sku is null)
            {
                return SkuTransitionCoordinationResult.SkuNotFound;
            }

            var product = await _products.Find(s, d => d.Id == sku.ProductId).FirstOrDefaultAsync(ct);
            if (product is null)
            {
                return SkuTransitionCoordinationResult.ProductNotFound;
            }

            if (product.Status == ProductStatus.Archived.ToString())
            {
                return SkuTransitionCoordinationResult.ProductArchived;
            }

            var touchFilter = Builders<ProductDocument>.Filter.Where(d =>
                d.Id == sku.ProductId
                && d.Status != ProductStatus.Archived.ToString()
                && d.LifecycleRevision == product.LifecycleRevision);

            var touchUpdate = Builders<ProductDocument>.Update.Inc(d => d.LifecycleRevision, 1);

            var touchResult = await _products.UpdateOneAsync(s, touchFilter, touchUpdate, options: null, ct);

            if (touchResult.MatchedCount == 0)
            {
                // Lost the race against a concurrent Archive.
                return SkuTransitionCoordinationResult.ProductArchived;
            }

            var skuFilter = Builders<SkuDocument>.Filter.Where(d =>
                d.Id == skuId.Value && d.Status == expectedCurrentStatus.ToString());

            var skuUpdate = Builders<SkuDocument>.Update.Set(d => d.Status, newStatus.ToString());

            var skuResult = await _skus.UpdateOneAsync(s, skuFilter, skuUpdate, options: null, ct);

            return skuResult.MatchedCount > 0
                ? SkuTransitionCoordinationResult.Transitioned
                : SkuTransitionCoordinationResult.ConcurrencyConflict;
        }, cancellationToken: cancellationToken);
    }
}
