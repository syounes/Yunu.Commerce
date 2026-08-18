using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.Products.CreateProduct;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class CreateProductHandlerTests
{
    private const int ValidGoogleCategoryId = 1234;
    private const string ValidGoogleCategoryPath = "Apparel & Accessories > Shoes > Athletic Shoes";

    private static FakeGoogleTaxonomyRepository CreateGoogleTaxonomyRepository(
        int googleCategoryId = ValidGoogleCategoryId,
        bool isActive = true,
        bool isLeaf = true)
    {
        var repository = new FakeGoogleTaxonomyRepository();

        repository.AddCategory(new GoogleTaxonomyCategoryResponse
        {
            GoogleCategoryId = googleCategoryId,
            ParentGoogleCategoryId = null,
            Name = "Athletic Shoes",
            FullPath = ValidGoogleCategoryPath,
            Level = 3,
            IsLeaf = isLeaf,
            IsActive = isActive
        });

        return repository;
    }

    [Fact]
    public async Task Handle_With_Valid_Command_Should_Persist_Product_And_Return_Generated_Id()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = Guid.NewGuid(),
            GoogleCategoryId = ValidGoogleCategoryId
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ProductId);
        Assert.Equal(1, repository.AddAsyncCallCount);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Apple iPhone 17 Pro", stored!.Name.Value);
        Assert.Equal(ValidGoogleCategoryId, stored.GoogleCategory.Id);
        Assert.Equal(ValidGoogleCategoryPath, stored.GoogleCategory.Path);
    }

    [Fact]
    public async Task Handle_With_Description_Should_Persist_Description()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "YUNU Runner",
            Description = "Tênis esportivo masculino para corrida e uso diário.",
            BrandId = Guid.NewGuid(),
            GoogleCategoryId = ValidGoogleCategoryId
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
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = Guid.NewGuid(),
            GoogleCategoryId = ValidGoogleCategoryId
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
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = null,
            GoogleCategoryId = ValidGoogleCategoryId
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
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = name!,
            BrandId = Guid.NewGuid(),
            GoogleCategoryId = ValidGoogleCategoryId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Empty_BrandId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            BrandId = Guid.Empty,
            GoogleCategoryId = ValidGoogleCategoryId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Invalid_GoogleCategoryId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = new FakeGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            GoogleCategoryId = 999999
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Inactive_GoogleCategory_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository(isActive: false);
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            GoogleCategoryId = ValidGoogleCategoryId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_NonLeaf_GoogleCategory_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository(isLeaf: false);
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            GoogleCategoryId = ValidGoogleCategoryId
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_Should_Use_FullPath_From_GoogleTaxonomy_Source()
    {
        var repository = new FakeProductRepository();
        var googleTaxonomyRepository = CreateGoogleTaxonomyRepository();
        var handler = new CreateProductHandler(repository, googleTaxonomyRepository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            GoogleCategoryId = ValidGoogleCategoryId
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.Equal(ValidGoogleCategoryPath, stored!.GoogleCategory.Path);
    }
}
