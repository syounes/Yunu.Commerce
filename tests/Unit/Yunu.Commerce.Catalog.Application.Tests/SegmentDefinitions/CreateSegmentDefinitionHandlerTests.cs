using Xunit;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions.CreateSegmentDefinition;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentDefinitions;

public class CreateSegmentDefinitionHandlerTests
{
    private static CreateSegmentDefinitionCommand ValidCommand(string code = "gender", string name = "Gender") => new()
    {
        Code = code,
        Name = name,
        Description = null,
        SemanticText = null,
        SelectionMode = "Single",
        AssignmentScope = "Product"
    };

    [Fact]
    public async Task Create_returns_generated_id()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var handler = new CreateSegmentDefinitionHandler(repo);

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.True(result.SegmentDefinitionId > 0);
    }

    [Fact]
    public async Task Create_rejects_duplicate_code()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var handler = new CreateSegmentDefinitionHandler(repo);

        await handler.HandleAsync(ValidCommand(code: "gender", name: "Gender"), CancellationToken.None);

        await Assert.ThrowsAsync<SegmentDefinitionConflictException>(() =>
            handler.HandleAsync(ValidCommand(code: "gender", name: "Other Gender"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_duplicate_normalized_name()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var handler = new CreateSegmentDefinitionHandler(repo);

        await handler.HandleAsync(ValidCommand(code: "gender", name: "Gender"), CancellationToken.None);

        await Assert.ThrowsAsync<SegmentDefinitionConflictException>(() =>
            handler.HandleAsync(ValidCommand(code: "other_gender", name: "  gender  "), CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_invalid_selection_mode()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var handler = new CreateSegmentDefinitionHandler(repo);

        var command = ValidCommand();
        var invalidCommand = new CreateSegmentDefinitionCommand
        {
            Code = command.Code,
            Name = command.Name,
            Description = command.Description,
            SemanticText = command.SemanticText,
            SelectionMode = "NotAMode",
            AssignmentScope = command.AssignmentScope
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(invalidCommand, CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_invalid_assignment_scope()
    {
        var repo = new FakeSegmentDefinitionRepository();
        var handler = new CreateSegmentDefinitionHandler(repo);

        var command = ValidCommand();
        var invalidCommand = new CreateSegmentDefinitionCommand
        {
            Code = command.Code,
            Name = command.Name,
            Description = command.Description,
            SemanticText = command.SemanticText,
            SelectionMode = command.SelectionMode,
            AssignmentScope = "NotAScope"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(invalidCommand, CancellationToken.None));
    }
}
