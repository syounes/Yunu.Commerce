namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionsByDefinition;

/// <summary>
/// Orchestrates retrieval of the Segment Options belonging to a Segment
/// Definition (docs task: "CQRS de leitura e endpoints GET para Segments e
/// Canonical Taxonomy" §1). Options of all Status values are preserved; it is
/// the API's responsibility to check that the parent Definition exists
/// before calling this handler.
/// </summary>
public sealed class GetSegmentOptionsByDefinitionHandler
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetSegmentOptionsByDefinitionHandler(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public Task<IReadOnlyCollection<SegmentOptionResponse>> HandleAsync(
        GetSegmentOptionsByDefinitionQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SegmentDefinitionId <= 0)
        {
            throw new ArgumentException("SegmentDefinitionId must be greater than zero.", nameof(query));
        }

        return _segmentCatalogRepository.GetOptionsByDefinitionAsync(query.SegmentDefinitionId, cancellationToken);
    }
}
