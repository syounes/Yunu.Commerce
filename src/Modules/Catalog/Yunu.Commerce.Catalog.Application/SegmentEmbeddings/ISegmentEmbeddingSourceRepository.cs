namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Port for reading the SQL Server source data used by the Segment embedding
/// synchronization pipeline (docs task: "Implementar sincronização de
/// embeddings de segmentos"). Distinct from
/// <see cref="Yunu.Commerce.Catalog.Application.SegmentCatalog.ISegmentCatalogRepository"/>,
/// which serves transactional assignment resolution and does not expose
/// SemanticText or UpdatedAt; this port loads the full active set used to
/// build semantic documents.
/// </summary>
public interface ISegmentEmbeddingSourceRepository
{
    /// <summary>
    /// Returns every active Segment Definition (Status = 'Active'), ordered
    /// deterministically by Code.
    /// </summary>
    Task<IReadOnlyCollection<SegmentDefinitionSource>> GetActiveDefinitionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active Segment Options whose owning Segment Definition is also
    /// active, ordered deterministically by Definition Code then Option Code.
    /// AssignmentScope is copied from the parent Definition.
    /// </summary>
    Task<IReadOnlyCollection<SegmentOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default);
}
