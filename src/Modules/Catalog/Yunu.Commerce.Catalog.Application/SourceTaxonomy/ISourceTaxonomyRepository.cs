namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy;

/// <summary>
/// Provider-neutral persistence/read port for SourceTaxonomy and
/// SourceTaxonomyNode (docs/adr/0014-provider-neutral-source-taxonomy.md).
/// Infrastructure implements this against SQL Server. The Application layer
/// never references SqlConnection or any other vendor-specific type.
///
/// SourceTaxonomy is imported/reference data, not a user-managed business
/// aggregate: this port intentionally exposes only simple internal
/// create/read semantics. Provider adapters, import orchestration and
/// upsert/deactivation synchronization belong to a later phase and are not
/// part of this contract.
///
/// CRITICAL: every node lookup that involves a node identity or
/// ExternalNodeId must remain scoped to SourceTaxonomyId. The same
/// ExternalNodeId may legitimately exist in different SourceTaxonomies
/// (Google, Mercado Livre, Amazon, client taxonomies, ...).
/// </summary>
public interface ISourceTaxonomyRepository
{
    Task<long> CreateAsync(
        SourceTaxonomyCreateRecord source,
        CancellationToken cancellationToken);

    Task<SourceTaxonomyDescriptorRecord?> GetByIdAsync(
        long sourceTaxonomyId,
        CancellationToken cancellationToken);

    Task<SourceTaxonomyDescriptorRecord?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SourceTaxonomyDescriptorRecord>> GetActiveAsync(
        CancellationToken cancellationToken);

    Task<long> CreateNodeAsync(
        SourceTaxonomyNodeCreateRecord node,
        CancellationToken cancellationToken);

    Task<SourceTaxonomyNodeRecord?> GetNodeByIdAsync(
        long sourceTaxonomyId,
        long sourceTaxonomyNodeId,
        CancellationToken cancellationToken);

    Task<SourceTaxonomyNodeRecord?> GetNodeByExternalIdAsync(
        long sourceTaxonomyId,
        string externalNodeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SourceTaxonomyNodeRecord>> GetRootsAsync(
        long sourceTaxonomyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SourceTaxonomyNodeRecord>> GetChildrenAsync(
        long sourceTaxonomyId,
        long parentSourceTaxonomyNodeId,
        CancellationToken cancellationToken);
}
