namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Write-side port for <see cref="SegmentDefinition"/> persistence. Distinct
/// from the read-side <c>ISegmentCatalogRepository</c> (Application layer),
/// which continues to serve queries/GET endpoints and must remain untouched.
/// Delete is intentionally not part of this port yet.
/// </summary>
public interface ISegmentDefinitionRepository
{
    Task<SegmentDefinitionId> AddAsync(SegmentDefinition definition, CancellationToken cancellationToken);

    Task UpdateAsync(SegmentDefinition definition, CancellationToken cancellationToken);

    Task<SegmentDefinition?> GetByIdAsync(SegmentDefinitionId id, CancellationToken cancellationToken);

    Task<SegmentDefinition?> GetByCodeAsync(SegmentDefinitionCode code, CancellationToken cancellationToken);

    Task<SegmentDefinition?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(SegmentDefinitionCode code, CancellationToken cancellationToken);
}
