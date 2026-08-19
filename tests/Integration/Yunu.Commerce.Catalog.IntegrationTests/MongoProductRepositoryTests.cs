using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;
using Xunit;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for MongoProductRepository against a real MongoDB instance
/// via Testcontainers (docs/architecture/06-solution-structure.md §42). Covers
/// only AddAsync/GetByIdAsync, matching the current IProductRepository contract.
///
/// Sku is no longer embedded in Product persistence
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md); Sku
/// round-tripping is covered by MongoSkuRepositoryTests.
/// </summary>
public sealed class MongoProductRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:8.0").Build();
    private IMongoClient _mongoClient = null!;
    private MongoProductRepository _repository = null!;

    private static CanonicalTaxonomyNodeId CreateCanonicalTaxonomyNodeId()
    {
        return new CanonicalTaxonomyNodeId(1234);
    }

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
    public async Task AddAsync_Then_GetByIdAsync_Should_RoundTrip_Product()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            "Apple's latest flagship smartphone.",
            BrandId.New(),
            CreateCanonicalTaxonomyNodeId());

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(product.Id, retrieved!.Id);
        Assert.Equal(product.Name, retrieved.Name);
        Assert.Equal(product.Description, retrieved.Description);
        Assert.Equal(product.BrandId, retrieved.BrandId);
        Assert.Equal(product.CanonicalTaxonomyNodeId, retrieved.CanonicalTaxonomyNodeId);
        Assert.Equal(product.Status, retrieved.Status);
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_Without_Description_Should_RoundTrip_Null()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("No Description Product"),
            description: null,
            BrandId.New(),
            CreateCanonicalTaxonomyNodeId());

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.Description);
    }

    [Fact]
    public async Task AddAsync_Then_GetByIdAsync_With_Null_BrandId_Should_RoundTrip_Null()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("No Internal Classification Product"),
            description: null,
            brandId: null,
            CreateCanonicalTaxonomyNodeId());

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.BrandId);
        Assert.Equal(product.CanonicalTaxonomyNodeId, retrieved.CanonicalTaxonomyNodeId);
    }

    [Fact]
    public async Task GetByIdAsync_For_NonExistent_Product_Should_Return_Null()
    {
        var retrieved = await _repository.GetByIdAsync(ProductId.New(), CancellationToken.None);

        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ProductStatus_Should_RoundTrip_As_Correct_Enum_Value()
    {
        var product = Product.Create(
            ProductId.New(),
            new ProductName("Status RoundTrip Product"),
            description: null,
            BrandId.New(),
            CreateCanonicalTaxonomyNodeId(),
            ProductStatus.PendingReview);

        await _repository.AddAsync(product, CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(product.Id, CancellationToken.None);

        Assert.NotNull(retrieved);
        Assert.Equal(ProductStatus.PendingReview, retrieved!.Status);
    }
}
