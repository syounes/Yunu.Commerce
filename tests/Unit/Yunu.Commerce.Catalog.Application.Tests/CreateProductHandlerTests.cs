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
