namespace Yunu.Commerce.Catalog.Application.SegmentCatalog;

/// <summary>
/// Port for resolving Segment reference data from SQL Server
/// (Catalog.SegmentDefinitions, Catalog.SegmentOptions -
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql,
/// deploy/databases/sqlserver/008-add-segment-assignment-scope.sql).
/// Catalog.Domain never accesses SQL Server directly; the Application layer
/// resolves and validates definitions/options through this port before
/// asking the Product/Sku Aggregate to assign a Segment (docs task:
/// "Canonical Taxonomy + Segments Domain" §24).
///
/// This step exposes Segments as read-only reference data (docs task §23):
/// no Create/Update/Delete is defined here.
/// </summary>
public interface ISegmentCatalogRepository
{
    Task<SegmentDefinitionResponse?> GetDefinitionByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<SegmentDefinitionResponse?> GetDefinitionByIdAsync(
        long segmentDefinitionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SegmentDefinitionResponse>> GetDefinitionsAsync(
        CancellationToken cancellationToken);

    Task<SegmentOptionResponse?> GetOptionAsync(
        long segmentDefinitionId,
        string optionCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SegmentOptionResponse>> GetOptionsByDefinitionAsync(
        long segmentDefinitionId,
        CancellationToken cancellationToken);
}
