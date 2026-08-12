using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for MongoSkuRepository against a real MongoDB instance via
/// Testcontainers (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// Covers AddAsync/GetByIdAsync/GetByProductIdAsync, matching the ISkuRepository
/// contract. Skus are persisted in their own "skus" collection, independent from
/// "products".
/// </summary>
public sealed class MongoSkuRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:8.0").Build();
    private IMongoClient _mongoClient = null!;
    private MongoSkuRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        _mongoClient = new MongoClient(_mongoContainer.GetConnectionString());

        var options = Options.Create(new CatalogMongoOptions
        {
            ConnectionString = _mongoContainer.GetConnectionString(),
            DatabaseName = "yunu_catalog_tests"
        });

        _repository = new MongoSkuRepository(_mongoClient, options);
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Sku()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("256GB-BLACK"), "0000000000001");

        await _repository.AddAsync(sku, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(sku.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(sku.Id, retrieved!.Id);
        Assert.Equal(sku.ProductId, retrieved.ProductId);
        Assert.Equal(sku.Code, retrieved.Code);
        Assert.Equal(sku.Gtin, retrieved.Gtin);
        Assert.Equal(sku.Status, retrieved.Status);
    }

    [Fact]
    public async Task GetByIdAsync_For_NonExistent_Sku_Should_Return_Null()
    {
        var retrieved = await _repository.GetByIdAsync(SkuId.New(), CancellationToken.None);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetByProductIdAsync_Should_Return_Only_Skus_For_Requested_Product()
    {
        var productId = ProductId.New();
        var otherProductId = ProductId.New();

        var sku1 = Sku.Create(SkuId.New(), productId, new SkuCode("256GB-BLACK"));
        var sku2 = Sku.Create(SkuId.New(), productId, new SkuCode("512GB-BLACK"));
        var otherSku = Sku.Create(SkuId.New(), otherProductId, new SkuCode("OTHER-SKU"));

        await _repository.AddAsync(sku1, CancellationToken.None);
        await _repository.AddAsync(sku2, CancellationToken.None);
        await _repository.AddAsync(otherSku, CancellationToken.None);

        var retrieved = await _repository.GetByProductIdAsync(productId, CancellationToken.None);

        Assert.Equal(2, retrieved.Count);
        Assert.All(retrieved, sku => Assert.Equal(productId, sku.ProductId));
    }

    [Fact]
    public async Task SkuStatus_Should_RoundTrip_As_Correct_Enum_Value()
    {
        var sku = Sku.Create(SkuId.New(), ProductId.New(), new SkuCode("STATUS-TEST"), status: SkuStatus.Active);

        await _repository.AddAsync(sku, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(sku.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(SkuStatus.Active, retrieved!.Status);
    }
}
