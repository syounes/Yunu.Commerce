using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Skus;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class GetProductByIdHandlerTests
{
    private const long ValidCanonicalTaxonomyNodeId = 1234;

    private static CanonicalTaxonomyNodeId CreateCanonicalTaxonomyNodeId()
    {
        return new CanonicalTaxonomyNodeId(ValidCanonicalTaxonomyNodeId);
    }

    private static GetProductByIdHandler CreateHandler(
        FakeProductRepository productRepository,
        FakeSkuRepository skuRepository)
    {
        var canonicalTaxonomyRepository = new FakeCanonicalTaxonomyRepository();
        var segmentCatalogRepository = new FakeSegmentCatalogRepository();

        var node = CanonicalTaxonomyNode.CreateRoot(
            CreateCanonicalTaxonomyNodeId(),
            "running_shoes",
            "Running Shoes",
            "RUNNING SHOES",
            description: null,
            path: "/catalog/fashion/shoes/athletic_shoes/running_shoes",
            status: CanonicalTaxonomyNodeStatus.Active);

        canonicalTaxonomyRepository.Add(ValidCanonicalTaxonomyNodeId, node);

        return new GetProductByIdHandler(productRepository, skuRepository, canonicalTaxonomyRepository, segmentCatalogRepository);
    }

    [Fact]
    public async Task Handle_With_Existing_Product_Should_Return_Mapped_Response()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();

        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            "Tênis esportivo masculino para corrida e uso diário.",
            BrandId.New(),
            CreateCanonicalTaxonomyNodeId());

        await productRepository.AddAsync(product, CancellationToken.None);

        var sku = Sku.Create(SkuId.New(), product.Id, new SkuCode("256GB-BLACK"));
        await skuRepository.AddAsync(sku, CancellationToken.None);

        var handler = CreateHandler(productRepository, skuRepository);
        var query = new GetProductByIdQuery { ProductId = product.Id.Value };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(product.Id.Value, response!.ProductId);
        Assert.Equal("Apple iPhone 17 Pro", response.Name);
        Assert.Equal("Tênis esportivo masculino para corrida e uso diário.", response.Description);
        Assert.Equal(product.BrandId!.Value.Value, response.BrandId);
        Assert.Equal(product.CanonicalTaxonomyNodeId.Value, response.Category.Id);
        Assert.Equal(ProductStatus.Draft.ToString(), response.Status);

        var skuResponse = Assert.Single(response.Skus);
        Assert.Equal("256GB-BLACK", skuResponse.Code);
        Assert.Equal(SkuStatus.Draft.ToString(), skuResponse.Status);
    }

    [Fact]
    public async Task Handle_With_Product_Without_Description_Should_Return_Null_Description()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();

        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            description: null,
            BrandId.New(),
            CreateCanonicalTaxonomyNodeId());

        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = CreateHandler(productRepository, skuRepository);
        var query = new GetProductByIdQuery { ProductId = product.Id.Value };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Description);
    }

    [Fact]
    public async Task Handle_With_Product_Without_BrandId_Should_Return_Null()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();

        var product = Product.Create(
            ProductId.New(),
            new ProductName("Apple iPhone 17 Pro"),
            description: null,
            brandId: null,
            CreateCanonicalTaxonomyNodeId());

        await productRepository.AddAsync(product, CancellationToken.None);

        var handler = CreateHandler(productRepository, skuRepository);
        var query = new GetProductByIdQuery { ProductId = product.Id.Value };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.BrandId);
    }

    [Fact]
    public async Task Handle_With_NonExistent_Product_Should_Return_Null()
    {
        var productRepository = new FakeProductRepository();
        var skuRepository = new FakeSkuRepository();
        var handler = CreateHandler(productRepository, skuRepository);

        var query = new GetProductByIdQuery { ProductId = Guid.NewGuid() };

        var response = await handler.HandleAsync(query, CancellationToken.None);

        Assert.Null(response);
    }
}
