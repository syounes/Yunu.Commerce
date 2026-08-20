namespace Yunu.Commerce.Catalog.Application.SegmentDefinitions.UpdateSegmentDefinition;

public sealed class UpdateSegmentDefinitionCommand
{
    public required long SegmentDefinitionId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? SemanticText { get; init; }

    public required string SelectionMode { get; init; }

    public required string AssignmentScope { get; init; }

    public required bool IsRequired { get; init; }

    public required string Status { get; init; }
}
