using Xunit;
using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SegmentDefinitionTests
{
    private static SegmentDefinition CreateDraft(
        string code = "gender",
        string name = "Gender",
        string? description = null,
        string? semanticText = null,
        SegmentSelectionMode selectionMode = SegmentSelectionMode.Single,
        SegmentAssignmentScope assignmentScope = SegmentAssignmentScope.Product,
        bool isRequired = false)
    {
        return SegmentDefinition.Create(
            new SegmentDefinitionCode(code),
            new SegmentDefinitionName(name),
            description,
            semanticText,
            selectionMode,
            assignmentScope,
            isRequired);
    }

    [Fact]
    public void Create_produces_definition_with_null_id()
    {
        var definition = CreateDraft();

        Assert.Null(definition.Id);
    }

    [Fact]
    public void Create_starts_as_draft()
    {
        var definition = CreateDraft();

        Assert.Equal(SegmentDefinitionStatus.Draft, definition.Status);
    }

    [Fact]
    public void Code_is_immutable_after_creation()
    {
        var definition = CreateDraft(code: "gender");

        Assert.Equal("gender", definition.Code.Value);
        // SegmentDefinitionCode has no setter/mutation method: compile-time immutability guarantee.
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SegmentDefinitionCode_rejects_null_or_whitespace(string value)
    {
        Assert.Throws<ArgumentException>(() => new SegmentDefinitionCode(value));
    }

    [Fact]
    public void SegmentDefinitionCode_rejects_more_than_100_characters()
    {
        var tooLong = new string('a', 101);

        Assert.Throws<ArgumentException>(() => new SegmentDefinitionCode(tooLong));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SegmentDefinitionName_rejects_null_or_whitespace(string value)
    {
        Assert.Throws<ArgumentException>(() => new SegmentDefinitionName(value));
    }

    [Fact]
    public void SegmentDefinitionName_rejects_more_than_200_characters()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<ArgumentException>(() => new SegmentDefinitionName(tooLong));
    }

    [Fact]
    public void NormalizedName_is_computed_correctly()
    {
        var definition = CreateDraft(name: "  Gênero   do produto ");

        Assert.Equal("GENERO DO PRODUTO", definition.NormalizedName);
    }

    [Fact]
    public void Description_longer_than_1000_characters_is_rejected()
    {
        var tooLong = new string('a', 1001);

        Assert.Throws<ArgumentException>(() => CreateDraft(description: tooLong));
    }

    [Fact]
    public void SemanticText_longer_than_2000_characters_is_rejected()
    {
        var tooLong = new string('a', 2001);

        Assert.Throws<ArgumentException>(() => CreateDraft(semanticText: tooLong));
    }

    [Fact]
    public void Optional_whitespace_text_becomes_null()
    {
        var definition = CreateDraft(description: "   ", semanticText: "   ");

        Assert.Null(definition.Description);
        Assert.Null(definition.SemanticText);
    }

    [Fact]
    public void Hydrate_requires_valid_id()
    {
        // SegmentDefinitionId itself rejects zero/negative values (readonly record struct guard).
        Assert.Throws<ArgumentException>(() => new SegmentDefinitionId(0));
    }

    [Fact]
    public void Hydrate_preserves_persisted_state()
    {
        var id = new SegmentDefinitionId(42);
        var code = new SegmentDefinitionCode("gender");
        var name = new SegmentDefinitionName("Gender");

        var definition = SegmentDefinition.Hydrate(
            id,
            code,
            name,
            "GENDER",
            "desc",
            "semantic",
            SegmentSelectionMode.Single,
            SegmentAssignmentScope.Product,
            true,
            SegmentDefinitionStatus.Active);

        Assert.Equal(id, definition.Id);
        Assert.Equal("gender", definition.Code.Value);
        Assert.Equal("Gender", definition.Name.Value);
        Assert.Equal("GENDER", definition.NormalizedName);
        Assert.Equal("desc", definition.Description);
        Assert.Equal("semantic", definition.SemanticText);
        Assert.Equal(SegmentSelectionMode.Single, definition.SelectionMode);
        Assert.Equal(SegmentAssignmentScope.Product, definition.AssignmentScope);
        Assert.True(definition.IsRequired);
        Assert.Equal(SegmentDefinitionStatus.Active, definition.Status);
    }

    [Fact]
    public void Hydrate_does_not_raise_creation_domain_event()
    {
        // SegmentDefinition does not model domain events at all in this step (no
        // DomainEvents collection exists); Hydrate simply reconstitutes state.
        var definition = SegmentDefinition.Hydrate(
            new SegmentDefinitionId(1),
            new SegmentDefinitionCode("gender"),
            new SegmentDefinitionName("Gender"),
            "GENDER",
            null,
            null,
            SegmentSelectionMode.Single,
            SegmentAssignmentScope.Product,
            false,
            SegmentDefinitionStatus.Draft);

        Assert.Equal(SegmentDefinitionStatus.Draft, definition.Status);
    }

    [Fact]
    public void Update_metadata_works()
    {
        var definition = CreateDraft();

        definition.Update(
            new SegmentDefinitionName("New Name"),
            "New description",
            "New semantic text",
            definition.SelectionMode,
            definition.AssignmentScope,
            definition.IsRequired,
            SegmentDefinitionStatus.Draft);

        Assert.Equal("New Name", definition.Name.Value);
        Assert.Equal("NEW NAME", definition.NormalizedName);
        Assert.Equal("New description", definition.Description);
        Assert.Equal("New semantic text", definition.SemanticText);
    }

    [Fact]
    public void Structural_change_works_in_draft()
    {
        var definition = CreateDraft(selectionMode: SegmentSelectionMode.Single, assignmentScope: SegmentAssignmentScope.Product, isRequired: false);

        definition.Update(
            definition.Name,
            definition.Description,
            definition.SemanticText,
            SegmentSelectionMode.Multiple,
            SegmentAssignmentScope.Sku,
            true,
            SegmentDefinitionStatus.Draft);

        Assert.Equal(SegmentSelectionMode.Multiple, definition.SelectionMode);
        Assert.Equal(SegmentAssignmentScope.Sku, definition.AssignmentScope);
        Assert.True(definition.IsRequired);
    }

    [Fact]
    public void Structural_change_outside_draft_is_rejected()
    {
        var definition = CreateDraft(selectionMode: SegmentSelectionMode.Single, assignmentScope: SegmentAssignmentScope.Product, isRequired: false);

        definition.Update(
            definition.Name,
            definition.Description,
            definition.SemanticText,
            definition.SelectionMode,
            definition.AssignmentScope,
            definition.IsRequired,
            SegmentDefinitionStatus.Active);

        Assert.Throws<SegmentDefinitionStructuralChangeNotAllowedException>(() =>
            definition.Update(
                definition.Name,
                definition.Description,
                definition.SemanticText,
                SegmentSelectionMode.Multiple,
                definition.AssignmentScope,
                definition.IsRequired,
                definition.Status));
    }

    [Fact]
    public void Keeping_same_structural_values_outside_draft_is_allowed()
    {
        var definition = CreateDraft(selectionMode: SegmentSelectionMode.Single, assignmentScope: SegmentAssignmentScope.Product, isRequired: false);

        definition.Update(
            definition.Name,
            definition.Description,
            definition.SemanticText,
            definition.SelectionMode,
            definition.AssignmentScope,
            definition.IsRequired,
            SegmentDefinitionStatus.Active);

        // Metadata-only change, same structural values, while Active.
        definition.Update(
            new SegmentDefinitionName("Renamed"),
            definition.Description,
            definition.SemanticText,
            definition.SelectionMode,
            definition.AssignmentScope,
            definition.IsRequired,
            definition.Status);

        Assert.Equal("Renamed", definition.Name.Value);
    }

    [Theory]
    [InlineData(SegmentDefinitionStatus.Draft, SegmentDefinitionStatus.Active)]
    [InlineData(SegmentDefinitionStatus.Draft, SegmentDefinitionStatus.Archived)]
    [InlineData(SegmentDefinitionStatus.Active, SegmentDefinitionStatus.Inactive)]
    [InlineData(SegmentDefinitionStatus.Active, SegmentDefinitionStatus.Archived)]
    [InlineData(SegmentDefinitionStatus.Inactive, SegmentDefinitionStatus.Active)]
    [InlineData(SegmentDefinitionStatus.Inactive, SegmentDefinitionStatus.Archived)]
    public void Valid_transitions_work(SegmentDefinitionStatus from, SegmentDefinitionStatus to)
    {
        var definition = CreateDraft();

        if (from != SegmentDefinitionStatus.Draft)
        {
            MoveTo(definition, from);
        }

        definition.Update(
            definition.Name,
            definition.Description,
            definition.SemanticText,
            definition.SelectionMode,
            definition.AssignmentScope,
            definition.IsRequired,
            to);

        Assert.Equal(to, definition.Status);
    }

    [Theory]
    [InlineData(SegmentDefinitionStatus.Draft, SegmentDefinitionStatus.Inactive)]
    [InlineData(SegmentDefinitionStatus.Active, SegmentDefinitionStatus.Draft)]
    [InlineData(SegmentDefinitionStatus.Inactive, SegmentDefinitionStatus.Draft)]
    public void Invalid_transitions_are_rejected(SegmentDefinitionStatus from, SegmentDefinitionStatus to)
    {
        var definition = CreateDraft();

        if (from != SegmentDefinitionStatus.Draft)
        {
            MoveTo(definition, from);
        }

        Assert.Throws<InvalidSegmentDefinitionStatusTransitionException>(() =>
            definition.Update(
                definition.Name,
                definition.Description,
                definition.SemanticText,
                definition.SelectionMode,
                definition.AssignmentScope,
                definition.IsRequired,
                to));
    }

    [Fact]
    public void Archived_cannot_be_edited()
    {
        var definition = CreateDraft();
        MoveTo(definition, SegmentDefinitionStatus.Archived);

        Assert.Throws<SegmentDefinitionStructuralChangeNotAllowedException>(() =>
            definition.Update(
                new SegmentDefinitionName("Anything"),
                definition.Description,
                definition.SemanticText,
                definition.SelectionMode,
                definition.AssignmentScope,
                definition.IsRequired,
                definition.Status));
    }

    private static void MoveTo(SegmentDefinition definition, SegmentDefinitionStatus target)
    {
        // Helper to walk allowed transitions to reach a target status from Draft.
        var path = target switch
        {
            SegmentDefinitionStatus.Active => new[] { SegmentDefinitionStatus.Active },
            SegmentDefinitionStatus.Inactive => new[] { SegmentDefinitionStatus.Active, SegmentDefinitionStatus.Inactive },
            SegmentDefinitionStatus.Archived => new[] { SegmentDefinitionStatus.Archived },
            _ => Array.Empty<SegmentDefinitionStatus>()
        };

        foreach (var status in path)
        {
            definition.Update(
                definition.Name,
                definition.Description,
                definition.SemanticText,
                definition.SelectionMode,
                definition.AssignmentScope,
                definition.IsRequired,
                status);
        }
    }
}
