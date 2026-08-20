using Xunit;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition;
using Yunu.Commerce.Catalog.Application.SegmentOptions;
using Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentOptions;

public class CreateSegmentOptionHandlerTests
{
    private static async Task<long> CreateDefinitionAsync(FakeSegmentDefinitionRepository definitionRepository)
    {
        var definitionHandler = new CreateSegmentDefinitionHandler(definitionRepository);
        var result = await definitionHandler.HandleAsync(new CreateSegmentDefinitionCommand
        {
            Code = "gender",
            Name = "Gender",
            Description = null,
            SemanticText = null,
            SelectionMode = "Single",
            AssignmentScope = "Product"
        }, CancellationToken.None);

        return result.SegmentDefinitionId;
    }

    private static CreateSegmentOptionCommand ValidCommand(long segmentDefinitionId, string code = "MALE", string name = "Masculino") => new()
    {
        SegmentDefinitionId = segmentDefinitionId,
        Code = code,
        Name = name,
        Description = null,
        SemanticText = null,
        DisplayOrder = 0
    };

    [Fact]
    public async Task Create_returns_generated_id()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var segmentDefinitionId = await CreateDefinitionAsync(definitionRepository);
        var handler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);

        var result = await handler.HandleAsync(ValidCommand(segmentDefinitionId), CancellationToken.None);

        Assert.True(result.SegmentOptionId > 0);
    }

    [Fact]
    public async Task Create_rejects_nonexistent_segment_definition()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var handler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.HandleAsync(ValidCommand(segmentDefinitionId: 999), CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_duplicate_code_within_same_definition()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var segmentDefinitionId = await CreateDefinitionAsync(definitionRepository);
        var handler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);

        await handler.HandleAsync(ValidCommand(segmentDefinitionId, code: "MALE", name: "Masculino"), CancellationToken.None);

        await Assert.ThrowsAsync<SegmentOptionConflictException>(() =>
            handler.HandleAsync(ValidCommand(segmentDefinitionId, code: "MALE", name: "Other Masculino"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_duplicate_normalized_name_within_same_definition()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var segmentDefinitionId = await CreateDefinitionAsync(definitionRepository);
        var handler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);

        await handler.HandleAsync(ValidCommand(segmentDefinitionId, code: "MALE", name: "Masculino"), CancellationToken.None);

        await Assert.ThrowsAsync<SegmentOptionConflictException>(() =>
            handler.HandleAsync(ValidCommand(segmentDefinitionId, code: "OTHER", name: "  masculino  "), CancellationToken.None));
    }

    [Fact]
    public async Task Create_allows_same_code_across_different_definitions()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var handler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);

        var firstDefinitionHandler = new CreateSegmentDefinitionHandler(definitionRepository);
        var firstDefinitionId = (await firstDefinitionHandler.HandleAsync(new CreateSegmentDefinitionCommand
        {
            Code = "gender",
            Name = "Gender",
            SelectionMode = "Single",
            AssignmentScope = "Product"
        }, CancellationToken.None)).SegmentDefinitionId;

        var secondDefinitionId = (await firstDefinitionHandler.HandleAsync(new CreateSegmentDefinitionCommand
        {
            Code = "target_audience",
            Name = "Target Audience",
            SelectionMode = "Single",
            AssignmentScope = "Product"
        }, CancellationToken.None)).SegmentDefinitionId;

        await handler.HandleAsync(ValidCommand(firstDefinitionId, code: "SHARED", name: "Shared Name A"), CancellationToken.None);

        var result = await handler.HandleAsync(ValidCommand(secondDefinitionId, code: "SHARED", name: "Shared Name B"), CancellationToken.None);

        Assert.True(result.SegmentOptionId > 0);
    }

    [Fact]
    public async Task Create_rejects_negative_display_order()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var segmentDefinitionId = await CreateDefinitionAsync(definitionRepository);
        var handler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);

        var command = new CreateSegmentOptionCommand
        {
            SegmentDefinitionId = segmentDefinitionId,
            Code = "MALE",
            Name = "Masculino",
            DisplayOrder = -1
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));
    }
}
