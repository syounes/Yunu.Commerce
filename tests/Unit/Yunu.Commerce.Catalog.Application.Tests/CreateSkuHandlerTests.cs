using Xunit;
using Yunu.Commerce.Catalog.Application.Skus.CreateSku;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Application.Tests;

public class CreateSkuHandlerTests
{
    [Fact]
    public async Task Handle_With_Valid_Command_Should_Persist_Sku_And_Return_Generated_Id()
    {
        var repository = new FakeSkuRepository();
        var handler = new CreateSkuHandler(repository);

        var command = new CreateSkuCommand
        {
            ProductId = Guid.NewGuid(),
            Code = "256GB-BLACK"
        };

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.SkuId);
        Assert.Equal(1, repository.AddAsyncCallCount);

        var stored = await repository.GetByIdAsync(new SkuId(result.SkuId), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("256GB-BLACK", stored!.Code.Value);
        Assert.Equal(command.ProductId, stored.ProductId.Value);
    }

    [Fact]
    public async Task Handle_With_Empty_ProductId_Should_Throw_And_Not_Persist()
    {
        var repository = new FakeSkuRepository();
        var handler = new CreateSkuHandler(repository);

        var command = new CreateSkuCommand
        {
            ProductId = Guid.Empty,
            Code = "256GB-BLACK"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_With_Invalid_Code_Should_Throw_And_Not_Persist(string? code)
    {
        var repository = new FakeSkuRepository();
        var handler = new CreateSkuHandler(repository);

        var command = new CreateSkuCommand
        {
            ProductId = Guid.NewGuid(),
            Code = code!
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, repository.AddAsyncCallCount);
    }
}
