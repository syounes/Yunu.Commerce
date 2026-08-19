namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionById;

/// <summary>
/// Orchestrates retrieval of a Segment Definition by identity
/// "CQRS de leitura e endpoints GET para Segments e Canonical Taxonomy" §1).
/// Returns null when the definition does not exist; does not throw for
/// absence.
/// </summary>
public sealed class GetSegmentDefinitionByIdHandler
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetSegmentDefinitionByIdHandler(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public Task<SegmentDefinitionResponse?> HandleAsync(
        GetSegmentDefinitionByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SegmentDefinitionId <= 0)
        {
            throw new ArgumentException("SegmentDefinitionId must be greater than zero.", nameof(query));
        }

        return _segmentCatalogRepository.GetDefinitionByIdAsync(query.SegmentDefinitionId, cancellationToken);
    }
}
