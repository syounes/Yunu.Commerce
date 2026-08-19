using Yunu.Commerce.Catalog.Application.Products.CreateProduct;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class CreateProductHandlerTests
{
    private const long ValidCanonicalTaxonomyNodeId = 1234;

    private static FakeCanonicalTaxonomyRepository CreateCanonicalTaxonomyRepository(
        long canonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId,
        CanonicalTaxonomyNodeStatus status = CanonicalTaxonomyNodeStatus.Active,
        bool isLeaf = true)
    {
        var repository = new FakeCanonicalTaxonomyRepository();

        var node = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(canonicalTaxonomyNodeId),
            "running_shoes",
            "Running Shoes",
            "RUNNING SHOES",
            description: null,
            path: "/catalog/fashion/shoes/athletic_shoes/running_shoes",
            status: status);

        repository.Add(canonicalTaxonomyNodeId, node);

        if (!isLeaf)
        {
            var child = CanonicalTaxonomyNode.CreateChild(
                new CanonicalTaxonomyNodeId(canonicalTaxonomyNodeId + 1),
                new CanonicalTaxonomyNodeId(canonicalTaxonomyNodeId),
                "running_shoes_child",
                "Running Shoes Child",
                "RUNNING SHOES CHILD",
                description: null,
                depth: 1,
                path: "/catalog/fashion/shoes/athletic_shoes/running_shoes/child");

            repository.Add(canonicalTaxonomyNodeId + 1, child);
        }

        return repository;
    }

    private static CreateProductHandler CreateHandler(
        FakeProductRepository productRepository,
        FakeCanonicalTaxonomyRepository canonicalTaxonomyRepository)
    {
        var segmentCatalogRepository = new FakeSegmentCatalogRepository();
        var segmentAssignmentResolver = new SegmentAssignmentResolver(segmentCatalogRepository);

        return new CreateProductHandler(productRepository, canonicalTaxonomyRepository, segmentAssignmentResolver);
    }

    [Fact]
    public async Task Handle_With_Valid_Command_Should_Persist_Product_And_Return_Generated_Id()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = Guid.NewGuid(),
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ProductId);
        Assert.Equal(1, repository.AddAsyncCallCount);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Apple iPhone 17 Pro", stored!.Name.Value);
        Assert.Equal(ValidCanonicalTaxonomyNodeId, stored.CanonicalTaxonomyNodeId.Value);
    }

    [Fact]
    public async Task Handle_With_Description_Should_Persist_Description()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "YUNU Runner",
            Description = "Tênis esportivo masculino para corrida e uso diário.",
            BrandId = Guid.NewGuid(),
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Tênis esportivo masculino para corrida e uso diário.", stored!.Description);
    }

    [Fact]
    public async Task Handle_Without_Description_Should_Persist_Null_Description()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = Guid.NewGuid(),
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored!.Description);
    }

    [Fact]
    public async Task Handle_With_Null_BrandId_Should_Persist_Null_BrandId()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = null,
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored!.BrandId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_With_Invalid_Name_Should_Throw_And_Not_Persist(string? name)
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = name!,
            BrandId = Guid.NewGuid(),
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Empty_BrandId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            BrandId = Guid.Empty,
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Invalid_CanonicalTaxonomyNodeId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = new FakeCanonicalTaxonomyRepository();
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            CanonicalTaxonomyNodeId = 999999
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Inactive_CanonicalTaxonomyNode_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository(status: CanonicalTaxonomyNodeStatus.Draft);
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_NonLeaf_CanonicalTaxonomyNode_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var canonicalTaxonomyRepository = CreateCanonicalTaxonomyRepository(isLeaf: false);
        var handler = CreateHandler(repository, canonicalTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            CanonicalTaxonomyNodeId = ValidCanonicalTaxonomyNodeId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }
}
