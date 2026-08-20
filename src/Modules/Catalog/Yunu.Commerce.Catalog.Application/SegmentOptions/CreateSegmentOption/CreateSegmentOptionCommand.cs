namespace Yunu.Commerce.Catalog.Application.SegmentOptions.CreateSegmentOption;

public sealed class CreateSegmentOptionCommand
{
    public required long SegmentDefinitionId { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? SemanticText { get; init; }

    public int DisplayOrder { get; init; }
}
