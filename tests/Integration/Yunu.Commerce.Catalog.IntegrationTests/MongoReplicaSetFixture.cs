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
/// not support them. This fixture mirrors the single-node replica-set
/// requirement already documented for local/dev Docker
/// (deploy/docker/docker-compose.yml): it starts the container with
/// <c>--replSet rs0</c>, deterministically initiates the replica set via
/// <c>mongosh</c> and then polls the real server state (no arbitrary sleeps)
/// until it reports a writable PRIMARY, i.e. transaction-ready.
///
/// Shared via <see cref="ICollectionFixture{TFixture}"/> across the
/// concurrency test class: starting a replica-set container is comparatively
/// expensive, and each test seeds its own Product/Sku documents with fresh
/// Guids, so sharing the same underlying database does not introduce
/// cross-test coupling.
/// </summary>
public sealed class MongoReplicaSetFixture : IAsyncLifetime
{
    private const string ReplicaSetName = "rs0";
    private const int MongoPort = 27017;

    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0.14")
        .WithCommand("--replSet", ReplicaSetName, "--bind_ip_all")
        .Build();

    public IMongoClient MongoClient { get; private set; } = null!;

    public MongoProductRepository ProductRepository { get; private set; } = null!;

    public MongoSkuRepository SkuRepository { get; private set; } = null!;

    public MongoProductSkuConcurrencyCoordinator Coordinator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var host = _container.Hostname;
        var mappedPort = _container.GetMappedPublicPort(MongoPort);

        await InitiateReplicaSetAsync(host, mappedPort);

        var connectionString = $"mongodb://{host}:{mappedPort}/?replicaSet={ReplicaSetName}";
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
    /// Deterministically initiates the single-node replica set. The member
    /// host is advertised as the same host:port the test process itself uses
    /// to connect (Testcontainers' mapped public port), so subsequent
    /// server-discovery/topology monitoring performed by the .NET driver from
    /// outside the container resolves to a reachable address.
    /// </summary>
    private async Task InitiateReplicaSetAsync(string host, int mappedPort)
    {
        var initiateEval =
            $"rs.initiate({{_id:'{ReplicaSetName}',members:[{{_id:0,host:'{host}:{mappedPort}'}}]}})";

        var result = await _container.ExecAsync(new[] { "mongosh", "--quiet", "--eval", initiateEval });

        if (result.ExitCode != 0 && !result.Stdout.Contains("already initialized", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Failed to initiate MongoDB replica set '{ReplicaSetName}'. Exit code: {result.ExitCode}. Stdout: {result.Stdout}. Stderr: {result.Stderr}.");
        }
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
