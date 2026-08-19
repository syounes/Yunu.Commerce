namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionById;

/// <summary>
/// Orchestrates retrieval of a Segment Option by identity, scoped to its
/// parent Segment Definition (docs task: "CQRS de leitura e endpoints GET
/// para Segments e Canonical Taxonomy" §1). Returns null when the option does
/// not exist or belongs to a different Definition.
/// </summary>
public sealed class GetSegmentOptionByIdHandler
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetSegmentOptionByIdHandler(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public Task<SegmentOptionResponse?> HandleAsync(
        GetSegmentOptionByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (query.SegmentDefinitionId <= 0)
        {
            throw new ArgumentException("SegmentDefinitionId must be greater than zero.", nameof(query));
        }

        if (query.SegmentOptionId <= 0)
        {
            throw new ArgumentException("SegmentOptionId must be greater than zero.", nameof(query));
        }

        return _segmentCatalogRepository.GetOptionByIdAsync(query.SegmentDefinitionId, query.SegmentOptionId, cancellationToken);
    }
}
