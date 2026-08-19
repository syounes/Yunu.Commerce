namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitions;

/// <summary>
/// Orchestrates retrieval of all Segment Definitions
/// leitura e endpoints GET para Segments e Canonical Taxonomy" §1). Read-only;
/// no filtering by Status is applied at this stage, since the response
/// already exposes Status.
/// </summary>
public sealed class GetSegmentDefinitionsHandler
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetSegmentDefinitionsHandler(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public Task<IReadOnlyCollection<SegmentDefinitionResponse>> HandleAsync(
        GetSegmentDefinitionsQuery query,
        CancellationToken cancellationToken)
    {
        return _segmentCatalogRepository.GetDefinitionsAsync(cancellationToken);
    }
}
