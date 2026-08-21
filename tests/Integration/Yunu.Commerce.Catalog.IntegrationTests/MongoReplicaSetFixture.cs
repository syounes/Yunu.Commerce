using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Shared Testcontainers fixture providing a real, single-node MongoDB
/// replica set (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
///
/// <see cref="MongoProductSkuConcurrencyCoordinator"/> uses
/// <c>session.WithTransactionAsync</c>, which requires MongoDB
/// multi-document transactions; a standalone (non-replica-set) mongod does
/// not support them. This fixture uses the Testcontainers.MongoDb module's
/// own supported replica-set configuration (<c>WithReplicaSet</c>), which
/// handles container startup, replica-set initiation and connection-string
/// construction; this fixture only adds a deterministic readiness check that
/// polls the real server state (no arbitrary sleeps) until it reports a
/// writable PRIMARY, i.e. transaction-ready, before any test runs.
///
/// Shared via <see cref="ICollectionFixture{TFixture}"/> across the
/// concurrency test class: starting a replica-set container is comparatively
/// expensive, and each test seeds its own Product/Sku documents with fresh
/// Guids, so sharing the same underlying database does not introduce
/// cross-test coupling.
/// </summary>
public sealed class MongoReplicaSetFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0.14")
        .WithReplicaSet()
        .Build();

    public IMongoClient MongoClient { get; private set; } = null!;

    public MongoProductRepository ProductRepository { get; private set; } = null!;

    public MongoSkuRepository SkuRepository { get; private set; } = null!;

    public MongoProductSkuConcurrencyCoordinator Coordinator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString();
        MongoClient = new MongoClient(connectionString);

        await WaitUntilWritablePrimaryAsync(MongoClient);

        var options = Options.Create(new CatalogMongoOptions
        {
            ConnectionString = connectionString,
            DatabaseName = "yunu_catalog_concurrency_tests"
        });

        ProductRepository = new MongoProductRepository(MongoClient, options);
        SkuRepository = new MongoSkuRepository(MongoClient, options);
        Coordinator = new MongoProductSkuConcurrencyCoordinator(MongoClient, options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Polls the real server state (via the "hello"/"isMaster" admin command)
    /// until this node reports itself as a writable PRIMARY, i.e. ready to
    /// accept <c>WithTransactionAsync</c>. Deterministic readiness check
    /// instead of an arbitrary fixed sleep.
    /// </summary>
    private static async Task WaitUntilWritablePrimaryAsync(IMongoClient mongoClient)
    {
        var admin = mongoClient.GetDatabase("admin");
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var reply = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));

                var isWritablePrimary =
                    (reply.Contains("isWritablePrimary") && reply["isWritablePrimary"].ToBoolean())
                    || (reply.Contains("ismaster") && reply["ismaster"].ToBoolean());

                if (isWritablePrimary)
                {
                    return;
                }
            }
            catch
            {
                // Server not ready to answer yet (e.g. still electing); retry until the deadline.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("MongoDB replica set did not become a writable PRIMARY within the expected time.");
    }
}

[CollectionDefinition(nameof(MongoReplicaSetCollection))]
public sealed class MongoReplicaSetCollection : ICollectionFixture<MongoReplicaSetFixture>
{
}
