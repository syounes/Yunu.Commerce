using Xunit;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition;
using Yunu.Commerce.Catalog.Application.SegmentOptions;
using Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption;
using Yunu.Commerce.Catalog.Application.SegmentOptions.UpdateSegmentOption;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentOptions;

public class UpdateSegmentOptionHandlerTests
{
    private static async Task<(long SegmentDefinitionId, long SegmentOptionId)> CreateOptionAsync(
        FakeSegmentDefinitionRepository definitionRepository,
        FakeSegmentOptionRepository optionRepository,
        string code = "MALE",
        string name = "Masculino")
    {
        var definitionHandler = new CreateSegmentDefinitionHandler(definitionRepository);
        var segmentDefinitionId = (await definitionHandler.HandleAsync(new CreateSegmentDefinitionCommand
        {
            Code = "gender",
            Name = "Gender",
            SelectionMode = "Single",
            AssignmentScope = "Product"
        }, CancellationToken.None)).SegmentDefinitionId;

        var createHandler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);
        var segmentOptionId = (await createHandler.HandleAsync(new CreateSegmentOptionCommand
        {
            SegmentDefinitionId = segmentDefinitionId,
            Code = code,
            Name = name
        }, CancellationToken.None)).SegmentOptionId;

        return (segmentDefinitionId, segmentOptionId);
    }

    [Fact]
    public async Task Update_changes_mutable_fields()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var (_, segmentOptionId) = await CreateOptionAsync(definitionRepository, optionRepository);
        var handler = new UpdateSegmentOptionHandler(optionRepository);

        await handler.HandleAsync(new UpdateSegmentOptionCommand
        {
            SegmentOptionId = segmentOptionId,
            Name = "Masculino Renomeado",
            Description = "Updated description",
            SemanticText = "Updated semantic text",
            DisplayOrder = 10,
            Status = "Active"
        }, CancellationToken.None);

        var updated = await optionRepository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId(segmentOptionId), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Masculino Renomeado", updated!.Name.Value);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal("Updated semantic text", updated.SemanticText);
        Assert.Equal(10, updated.DisplayOrder);
        Assert.Equal(Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionStatus.Active, updated.Status);
    }

    [Fact]
    public async Task Update_rejects_nonexistent_option()
    {
        var optionRepository = new FakeSegmentOptionRepository();
        var handler = new UpdateSegmentOptionHandler(optionRepository);

        var command = new UpdateSegmentOptionCommand
        {
            SegmentOptionId = 999,
            Name = "Masculino",
            Status = "Draft"
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_rejects_duplicate_normalized_name_within_same_definition()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var (segmentDefinitionId, _) = await CreateOptionAsync(definitionRepository, optionRepository, code: "MALE", name: "Masculino");

        var createHandler = new CreateSegmentOptionHandler(optionRepository, definitionRepository);
        var secondOptionId = (await createHandler.HandleAsync(new CreateSegmentOptionCommand
        {
            SegmentDefinitionId = segmentDefinitionId,
            Code = "FEMALE",
            Name = "Feminino"
        }, CancellationToken.None)).SegmentOptionId;

        var handler = new UpdateSegmentOptionHandler(optionRepository);
        var command = new UpdateSegmentOptionCommand
        {
            SegmentOptionId = secondOptionId,
            Name = "  masculino  ",
            Status = "Draft"
        };

        await Assert.ThrowsAsync<SegmentOptionConflictException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_rejects_invalid_status()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var (_, segmentOptionId) = await CreateOptionAsync(definitionRepository, optionRepository);
        var handler = new UpdateSegmentOptionHandler(optionRepository);

        var command = new UpdateSegmentOptionCommand
        {
            SegmentOptionId = segmentOptionId,
            Name = "Masculino",
            Status = "NotAStatus"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_does_not_change_segment_definition_id()
    {
        var definitionRepository = new FakeSegmentDefinitionRepository();
        var optionRepository = new FakeSegmentOptionRepository();
        var (segmentDefinitionId, segmentOptionId) = await CreateOptionAsync(definitionRepository, optionRepository);
        var handler = new UpdateSegmentOptionHandler(optionRepository);

        await handler.HandleAsync(new UpdateSegmentOptionCommand
        {
            SegmentOptionId = segmentOptionId,
            Name = "Masculino",
            Status = "Active"
        }, CancellationToken.None);

        var updated = await optionRepository.GetByIdAsync(new Yunu.Commerce.Catalog.Domain.Segments.SegmentOptionId(segmentOptionId), CancellationToken.None);

        Assert.Equal(new Yunu.Commerce.Catalog.Domain.Segments.SegmentDefinitionId(segmentDefinitionId), updated!.SegmentDefinitionId);
    }
}
