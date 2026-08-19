namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionById;

public sealed class GetSegmentOptionByIdQuery
{
    public required long SegmentDefinitionId { get; init; }

    public required long SegmentOptionId { get; init; }
}
