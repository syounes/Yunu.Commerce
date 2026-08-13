using Yunu.Commerce.Catalog.Application.Products.CreateProduct;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class CreateProductHandlerTests
{
    [Fact]
    public async Task Handle_With_Valid_Command_Should_Persist_Product_And_Return_Generated_Id()
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductHandler(repository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid()
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ProductId);
        Assert.Equal(1, repository.AddAsyncCallCount);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Apple iPhone 17 Pro", stored!.Name.Value);
    }

    [Fact]
    public async Task Handle_With_Description_Should_Persist_Description()
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductHandler(repository);

        var command = new CreateProductCommand
        {
            Name = "YUNU Runner",
            Description = "Tênis esportivo masculino para corrida e uso diário.",
            BrandId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid()
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
        var handler = new CreateProductHandler(repository);

        var command = new CreateProductCommand
        {
            Name = "Apple iPhone 17 Pro",
            BrandId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid()
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        var stored = await repository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Products.ProductId(result.ProductId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored!.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_With_Invalid_Name_Should_Throw_And_Not_Persist(string? name)
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductHandler(repository);

        var command = new CreateProductCommand
        {
            Name = name!,
            BrandId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Empty_BrandId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductHandler(repository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            BrandId = Guid.Empty,
            CategoryId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Fact]
    public async Task Handle_With_Empty_CategoryId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductHandler(repository);

        var command = new CreateProductCommand
        {
            Name = "Valid Name",
            BrandId = Guid.NewGuid(),
            CategoryId = Guid.Empty
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }
}
