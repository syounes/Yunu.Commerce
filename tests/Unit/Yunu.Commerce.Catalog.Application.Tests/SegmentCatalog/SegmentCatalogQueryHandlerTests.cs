using Xunit;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionByCode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionById;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitions;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionByCode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionById;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionsByDefinition;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentCatalog;

public class SegmentCatalogQueryHandlerTests
{
    private static SegmentDefinitionResponse CreateDefinition(long id, string code) => new()
    {
        SegmentDefinitionId = id,
        Code = code,
        Name = code,
        SelectionMode = "Single",
        AssignmentScope = "Product",
        IsRequired = false,
        Status = "Active"
    };

    private static SegmentOptionResponse CreateOption(long optionId, long definitionId, string code, int displayOrder = 0) => new()
    {
        SegmentOptionId = optionId,
        SegmentDefinitionId = definitionId,
        Code = code,
        Name = code,
        DisplayOrder = displayOrder,
        Status = "Active"
    };

    [Fact]
    public async Task GetSegmentDefinitions_Should_Return_All_Definitions()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddDefinition(CreateDefinition(1, "gender"));
        repo.AddDefinition(CreateDefinition(2, "target_audience"));

        var handler = new GetSegmentDefinitionsHandler(repo);
        var result = await handler.HandleAsync(new GetSegmentDefinitionsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetSegmentDefinitionById_Should_Return_Definition_When_Found()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddDefinition(CreateDefinition(1, "gender"));

        var handler = new GetSegmentDefinitionByIdHandler(repo);
        var result = await handler.HandleAsync(new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = 1 }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("gender", result!.Code);
    }

    [Fact]
    public async Task GetSegmentDefinitionById_Should_Return_Null_When_Not_Found()
    {
        var repo = new FakeSegmentCatalogRepository();
        var handler = new GetSegmentDefinitionByIdHandler(repo);

        var result = await handler.HandleAsync(new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = 999 }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSegmentDefinitionById_Should_Reject_Invalid_Id()
    {
        var repo = new FakeSegmentCatalogRepository();
        var handler = new GetSegmentDefinitionByIdHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = 0 }, CancellationToken.None));
    }

    [Fact]
    public async Task GetSegmentDefinitionByCode_Should_Return_Definition_When_Found()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddDefinition(CreateDefinition(1, "gender"));

        var handler = new GetSegmentDefinitionByCodeHandler(repo);
        var result = await handler.HandleAsync(new GetSegmentDefinitionByCodeQuery { Code = "gender" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result!.SegmentDefinitionId);
    }

    [Fact]
    public async Task GetSegmentDefinitionByCode_Should_Reject_Empty_Code()
    {
        var repo = new FakeSegmentCatalogRepository();
        var handler = new GetSegmentDefinitionByCodeHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentDefinitionByCodeQuery { Code = "   " }, CancellationToken.None));
    }

    [Fact]
    public async Task GetSegmentOptionsByDefinition_Should_Return_Options_For_That_Definition()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddOption(CreateOption(10, 1, "male", 0));
        repo.AddOption(CreateOption(11, 1, "female", 1));
        repo.AddOption(CreateOption(12, 2, "teen", 0));

        var handler = new GetSegmentOptionsByDefinitionHandler(repo);
        var result = await handler.HandleAsync(new GetSegmentOptionsByDefinitionQuery { SegmentDefinitionId = 1 }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.Equal(1, o.SegmentDefinitionId));
    }

    [Fact]
    public async Task GetSegmentOptionsByDefinition_Should_Reject_Invalid_Id()
    {
        var repo = new FakeSegmentCatalogRepository();
        var handler = new GetSegmentOptionsByDefinitionHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentOptionsByDefinitionQuery { SegmentDefinitionId = -1 }, CancellationToken.None));
    }

    [Fact]
    public async Task GetSegmentOptionById_Should_Return_Option_When_Found()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddOption(CreateOption(10, 1, "male"));

        var handler = new GetSegmentOptionByIdHandler(repo);
        var result = await handler.HandleAsync(
            new GetSegmentOptionByIdQuery { SegmentDefinitionId = 1, SegmentOptionId = 10 },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("male", result!.Code);
    }

    [Fact]
    public async Task GetSegmentOptionById_Should_Not_Return_Option_Belonging_To_Another_Definition()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddOption(CreateOption(10, 1, "male"));

        var handler = new GetSegmentOptionByIdHandler(repo);
        var result = await handler.HandleAsync(
            new GetSegmentOptionByIdQuery { SegmentDefinitionId = 2, SegmentOptionId = 10 },
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSegmentOptionById_Should_Reject_Invalid_Ids()
    {
        var repo = new FakeSegmentCatalogRepository();
        var handler = new GetSegmentOptionByIdHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentOptionByIdQuery { SegmentDefinitionId = 0, SegmentOptionId = 1 }, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentOptionByIdQuery { SegmentDefinitionId = 1, SegmentOptionId = 0 }, CancellationToken.None));
    }

    [Fact]
    public async Task GetSegmentOptionByCode_Should_Return_Option_When_Found()
    {
        var repo = new FakeSegmentCatalogRepository();
        repo.AddOption(CreateOption(10, 1, "male"));

        var handler = new GetSegmentOptionByCodeHandler(repo);
        var result = await handler.HandleAsync(
            new GetSegmentOptionByCodeQuery { SegmentDefinitionId = 1, OptionCode = "male" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(10, result!.SegmentOptionId);
    }

    [Fact]
    public async Task GetSegmentOptionByCode_Should_Reject_Invalid_Inputs()
    {
        var repo = new FakeSegmentCatalogRepository();
        var handler = new GetSegmentOptionByCodeHandler(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentOptionByCodeQuery { SegmentDefinitionId = 0, OptionCode = "male" }, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(new GetSegmentOptionByCodeQuery { SegmentDefinitionId = 1, OptionCode = "" }, CancellationToken.None));
    }
}
