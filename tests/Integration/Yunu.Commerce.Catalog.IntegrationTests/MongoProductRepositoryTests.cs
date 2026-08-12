using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Categories;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Products.Skus;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for MongoProductRepository against a real MongoDB instance
/// via Testcontainers (docs/architecture/06-solution-structure.md §42). Covers
/// only AddAsync/GetByIdAsync, matching the current IProductRepository contract.
/// </summary>
public sealed class MongoProductRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:8.0").Build();
    private IMongoClient _mongoClient = null!;
    private MongoProductRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        _mongoClient = new MongoClient(_mongoContainer.GetConnectionString());

        var options = Options.Create(new CatalogMongoOptions
        {
            ConnectionString = _mongoContainer.GetConnectionString(),
            DatabaseName = "yunu_catalog_tests"
        });

        _repository = new MongoProductRepository(_mongoClient, options);
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Product_Without_Skus()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            BrandId.New(),
            CategoryId.New());

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(product.Id, retrieved!.Id);
        Assert.Equal(product.Name, retrieved.Name);
        Assert.Equal(product.BrandId, retrieved.BrandId);
        Assert.Equal(product.CategoryId, retrieved.CategoryId);
        Assert.Equal(product.Status, retrieved.Status);
        Assert.Empty(retrieved.Skus);
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Product_With_Skus()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            BrandId.New(),
            CategoryId.New());

        product.AddSku(SkuId.New(), new SkuCode("256GB-BLACK"), SkuStatus.Draft);
        product.AddSku(SkuId.New(), new SkuCode("512GB-BLACK"), SkuStatus.Active);

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(2, retrieved!.Skus.Count);

        var originalSkus = product.Skus.OrderBy(s => s.Id.Value).ToList();
        var retrievedSkus = retrieved.Skus.OrderBy(s => s.Id.Value).ToList();

        for (var i = 0; i < originalSkus.Count; i++)
        {
            Assert.Equal(originalSkus[i].Id, retrievedSkus[i].Id);
            Assert.Equal(originalSkus[i].Code, retrievedSkus[i].Code);
            Assert.Equal(originalSkus[i].Status, retrievedSkus[i].Status);
        }
    }

    [Fact]
    public async Task GetByIdAsync_For_NonExistent_Product_Should_Return_Null()
    {
        var retrieved = await _repository.GetByIdAsync(ProductId.New(), CancellationToken.None);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ProductStatus_And_SkuStatus_Should_RoundTrip_As_Correct_Enum_Values()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("Status RoundTrip Product"),
            BrandId.New(),
            CategoryId.New(),
            ProductStatus.PendingReview);

        product.AddSku(SkuId.New(), new SkuCode("SKU-STATUS-TEST"), SkuStatus.Inactive);

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(ProductStatus.PendingReview, retrieved!.Status);
        Assert.Equal(SkuStatus.Inactive, retrieved.Skus.Single().Status);
    }
}
