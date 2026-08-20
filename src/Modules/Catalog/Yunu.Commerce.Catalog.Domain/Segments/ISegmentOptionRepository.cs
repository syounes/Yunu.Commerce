namespace Yunu.Commerce.Catalog.Domain.Segments;

/// <summary>
/// Write-side port for <see cref="SegmentOption"/> persistence. Distinct
/// from the read-side <c>ISegmentCatalogRepository</c> (Application layer),
/// which continues to serve queries/GET endpoints and must remain untouched.
/// Mirrors <see cref="ISegmentDefinitionRepository"/>. Delete is
/// intentionally not part of this port yet.
/// </summary>
public interface ISegmentOptionRepository
{
    Task<SegmentOptionId> AddAsync(SegmentOption option, CancellationToken cancellationToken);

    Task UpdateAsync(SegmentOption option, CancellationToken cancellationToken);

    Task<SegmentOption?> GetByIdAsync(SegmentOptionId id, CancellationToken cancellationToken);

    Task<SegmentOption?> GetByCodeAsync(SegmentDefinitionId segmentDefinitionId, SegmentOptionCode code, CancellationToken cancellationToken);

    Task<SegmentOption?> FindByNormalizedNameAsync(SegmentDefinitionId segmentDefinitionId, string normalizedName, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(SegmentDefinitionId segmentDefinitionId, SegmentOptionCode code, CancellationToken cancellationToken);
}
