namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionByCode;

public sealed class GetSegmentOptionByCodeQuery
{
    public required long SegmentDefinitionId { get; init; }

    public required string OptionCode { get; init; }
}
