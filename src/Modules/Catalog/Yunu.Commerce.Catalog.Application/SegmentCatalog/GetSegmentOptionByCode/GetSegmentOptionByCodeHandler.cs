namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionByCode;

/// <summary>
/// Orchestrates retrieval of a Segment Option by Code
/// Segment Definition (docs task: "CQRS de leitura e endpoints GET para
/// Segments e Canonical Taxonomy" §1). Reuses the existing
/// <see cref="ISegmentCatalogRepository.GetOptionAsync"/> behavior used by
/// <see cref="SegmentAssignmentResolver"/>.
/// </summary>
public sealed class GetSegmentOptionByCodeHandler
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetSegmentOptionByCodeHandler(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public Task<SegmentOptionResponse?> HandleAsync(
        GetSegmentOptionByCodeQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SegmentDefinitionId <= 0)
        {
            throw new ArgumentException("SegmentDefinitionId must be greater than zero.", nameof(query));
        }

        if (string.IsNullOrWhiteSpace(query.OptionCode))
        {
            throw new ArgumentException("OptionCode cannot be null, empty or whitespace.", nameof(query));
        }

        return _segmentCatalogRepository.GetOptionAsync(query.SegmentDefinitionId, query.OptionCode.Trim(), cancellationToken);
    }
}
