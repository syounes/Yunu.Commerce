namespace Yunu.Commerce.Catalog.Application.SegmentOptions.UpdateSegmentOption;

public sealed class UpdateSegmentOptionCommand
{
    public required long SegmentOptionId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? SemanticText { get; init; }

    public int DisplayOrder { get; init; }

    public required string Status { get; init; }
}
