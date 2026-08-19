namespace Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionByCode;

/// <summary>
/// Orchestrates retrieval of a Segment Definition by Code
/// de leitura e endpoints GET para Segments e Canonical Taxonomy" §1).
/// Returns null when the definition does not exist.
/// </summary>
public sealed class GetSegmentDefinitionByCodeHandler
{
    private readonly ISegmentCatalogRepository _segmentCatalogRepository;

    public GetSegmentDefinitionByCodeHandler(ISegmentCatalogRepository segmentCatalogRepository)
    {
        _segmentCatalogRepository = segmentCatalogRepository;
    }

    public Task<SegmentDefinitionResponse?> HandleAsync(
        GetSegmentDefinitionByCodeQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Code))
        {
            throw new ArgumentException("Code cannot be null, empty or whitespace.", nameof(query));
        }

        return _segmentCatalogRepository.GetDefinitionByCodeAsync(query.Code.Trim(), cancellationToken);
    }
}
