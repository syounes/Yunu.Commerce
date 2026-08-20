using Xunit;
using Yunu.Commerce.Catalog.Domain.Segments;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class SegmentOptionTests
{
    private static SegmentOption CreateDraft(
        long segmentDefinitionId = 1,
        string code = "MALE",
        string name = "Masculino",
        string? description = null,
        string? semanticText = null,
        int displayOrder = 0)
    {
        return SegmentOption.Create(
            new SegmentDefinitionId(segmentDefinitionId),
            new SegmentOptionCode(code),
            new SegmentOptionName(name),
            description,
            semanticText,
            displayOrder);
    }

    [Fact]
    public void Create_produces_option_with_null_id()
    {
        var option = CreateDraft();

        Assert.Null(option.Id);
    }

    [Fact]
    public void Create_starts_as_draft()
    {
        var option = CreateDraft();

        Assert.Equal(SegmentOptionStatus.Draft, option.Status);
    }

    [Fact]
    public void Create_assigns_segment_definition_id()
    {
        var option = CreateDraft(segmentDefinitionId: 42);

        Assert.Equal(new SegmentDefinitionId(42), option.SegmentDefinitionId);
    }

    [Fact]
    public void Code_is_immutable_after_creation()
    {
        var option = CreateDraft(code: "MALE");

        Assert.Equal("MALE", option.Code.Value);
        // SegmentOptionCode has no setter/mutation method: compile-time immutability guarantee.
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SegmentOptionCode_rejects_null_or_whitespace(string value)
    {
        Assert.Throws<ArgumentException>(() => new SegmentOptionCode(value));
    }

    [Fact]
    public void SegmentOptionCode_rejects_more_than_100_characters()
    {
        var tooLong = new string('a', 101);

        Assert.Throws<ArgumentException>(() => new SegmentOptionCode(tooLong));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SegmentOptionName_rejects_null_or_whitespace(string value)
    {
        Assert.Throws<ArgumentException>(() => new SegmentOptionName(value));
    }

    [Fact]
    public void SegmentOptionName_rejects_more_than_200_characters()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<ArgumentException>(() => new SegmentOptionName(tooLong));
    }

    [Fact]
    public void NormalizedName_is_computed_correctly()
    {
        var option = CreateDraft(name: "  Gênero   Masculino ");

        Assert.Equal("GENERO MASCULINO", option.NormalizedName);
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
        var option = CreateDraft(description: "   ", semanticText: "   ");

        Assert.Null(option.Description);
        Assert.Null(option.SemanticText);
    }

    [Fact]
    public void Create_rejects_negative_display_order()
    {
        Assert.Throws<ArgumentException>(() => CreateDraft(displayOrder: -1));
    }

    [Fact]
    public void Hydrate_requires_valid_id()
    {
        // SegmentOptionId itself rejects zero/negative values (readonly record struct guard).
        Assert.Throws<ArgumentException>(() => new SegmentOptionId(0));
    }

    [Fact]
    public void Hydrate_preserves_persisted_state()
    {
        var id = new SegmentOptionId(99);
        var segmentDefinitionId = new SegmentDefinitionId(42);
        var code = new SegmentOptionCode("MALE");
        var name = new SegmentOptionName("Masculino");

        var option = SegmentOption.Hydrate(
            id,
            segmentDefinitionId,
            code,
            name,
            "MASCULINO",
            "desc",
            "semantic",
            10,
            SegmentOptionStatus.Active);

        Assert.Equal(id, option.Id);
        Assert.Equal(segmentDefinitionId, option.SegmentDefinitionId);
        Assert.Equal("MALE", option.Code.Value);
        Assert.Equal("Masculino", option.Name.Value);
        Assert.Equal("MASCULINO", option.NormalizedName);
        Assert.Equal("desc", option.Description);
        Assert.Equal("semantic", option.SemanticText);
        Assert.Equal(10, option.DisplayOrder);
        Assert.Equal(SegmentOptionStatus.Active, option.Status);
    }

    [Fact]
    public void AssignIdentity_sets_id_once()
    {
        var option = CreateDraft();
        var id = new SegmentOptionId(1);

        option.AssignIdentity(id);

        Assert.Equal(id, option.Id);
    }

    [Fact]
    public void AssignIdentity_throws_when_already_assigned()
    {
        var option = CreateDraft();
        option.AssignIdentity(new SegmentOptionId(1));

        Assert.Throws<InvalidOperationException>(() => option.AssignIdentity(new SegmentOptionId(2)));
    }

    [Fact]
    public void Update_changes_mutable_fields()
    {
        var option = CreateDraft(name: "Masculino");

        option.Update(
            new SegmentOptionName("Masculino Renomeado"),
            "New description",
            "New semantic text",
            20,
            SegmentOptionStatus.Draft);

        Assert.Equal("Masculino Renomeado", option.Name.Value);
        Assert.Equal("MASCULINO RENOMEADO", option.NormalizedName);
        Assert.Equal("New description", option.Description);
        Assert.Equal("New semantic text", option.SemanticText);
        Assert.Equal(20, option.DisplayOrder);
    }

    [Fact]
    public void Update_does_not_change_id_or_segment_definition_id()
    {
        var option = CreateDraft(segmentDefinitionId: 5);
        option.AssignIdentity(new SegmentOptionId(1));

        option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, option.Status);

        Assert.Equal(new SegmentOptionId(1), option.Id);
        Assert.Equal(new SegmentDefinitionId(5), option.SegmentDefinitionId);
    }

    [Fact]
    public void Update_rejects_negative_display_order()
    {
        var option = CreateDraft();

        Assert.Throws<ArgumentException>(() => option.Update(option.Name, option.Description, option.SemanticText, -1, option.Status));
    }

    [Fact]
    public void Update_throws_when_archived()
    {
        var option = CreateDraft();
        option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, SegmentOptionStatus.Archived);

        Assert.Throws<InvalidSegmentOptionStatusTransitionException>(() =>
            option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, SegmentOptionStatus.Active));
    }

    [Theory]
    [InlineData(SegmentOptionStatus.Draft, SegmentOptionStatus.Active)]
    [InlineData(SegmentOptionStatus.Draft, SegmentOptionStatus.Archived)]
    [InlineData(SegmentOptionStatus.Active, SegmentOptionStatus.Inactive)]
    [InlineData(SegmentOptionStatus.Active, SegmentOptionStatus.Archived)]
    [InlineData(SegmentOptionStatus.Inactive, SegmentOptionStatus.Active)]
    [InlineData(SegmentOptionStatus.Inactive, SegmentOptionStatus.Archived)]
    public void Update_allows_valid_transitions(SegmentOptionStatus from, SegmentOptionStatus to)
    {
        var option = CreateDraft();
        MoveToStatus(option, from);

        option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, to);

        Assert.Equal(to, option.Status);
    }

    private static void MoveToStatus(SegmentOption option, SegmentOptionStatus target)
    {
        if (option.Status == target)
        {
            return;
        }

        if (target == SegmentOptionStatus.Inactive)
        {
            // Inactive is only reachable from Active.
            option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, SegmentOptionStatus.Active);
        }

        option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, target);
    }

    [Theory]
    [InlineData(SegmentOptionStatus.Draft, SegmentOptionStatus.Inactive)]
    [InlineData(SegmentOptionStatus.Active, SegmentOptionStatus.Draft)]
    [InlineData(SegmentOptionStatus.Inactive, SegmentOptionStatus.Draft)]
    public void Update_rejects_invalid_transitions(SegmentOptionStatus from, SegmentOptionStatus to)
    {
        var option = CreateDraft();
        MoveToStatus(option, from);

        Assert.Throws<InvalidSegmentOptionStatusTransitionException>(() =>
            option.Update(option.Name, option.Description, option.SemanticText, option.DisplayOrder, to));
    }
}
