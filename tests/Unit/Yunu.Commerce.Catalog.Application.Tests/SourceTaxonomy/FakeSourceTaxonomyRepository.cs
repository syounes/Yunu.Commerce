using Yunu.Commerce.Catalog.Application.SourceTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

/// <summary>
/// Minimal in-memory fake for <see cref="ISourceTaxonomyRepository"/>, used
/// only for orchestrator unit tests. Only members required by
/// SourceTaxonomyImportOrchestrator are meaningfully implemented.
/// </summary>
public sealed class FakeSourceTaxonomyRepository : ISourceTaxonomyRepository
{
    private readonly Dictionary<long, SourceTaxonomyDescriptorRecord> _sources = new();

    public void Seed(SourceTaxonomyDescriptorRecord descriptor) => _sources[descriptor.SourceTaxonomyId] = descriptor;

    public Task<long> CreateAsync(SourceTaxonomyCreateRecord source, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<SourceTaxonomyDescriptorRecord?> GetByIdAsync(long sourceTaxonomyId, CancellationToken cancellationToken)
        => Task.FromResult(_sources.TryGetValue(sourceTaxonomyId, out var descriptor) ? descriptor : null);

    public Task<SourceTaxonomyDescriptorRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<IReadOnlyCollection<SourceTaxonomyDescriptorRecord>> GetActiveAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<long> CreateNodeAsync(SourceTaxonomyNodeCreateRecord node, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<SourceTaxonomyNodeRecord?> GetNodeByIdAsync(long sourceTaxonomyId, long sourceTaxonomyNodeId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<SourceTaxonomyNodeRecord?> GetNodeByExternalIdAsync(long sourceTaxonomyId, string externalNodeId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<IReadOnlyCollection<SourceTaxonomyNodeRecord>> GetRootsAsync(long sourceTaxonomyId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<IReadOnlyCollection<SourceTaxonomyNodeRecord>> GetChildrenAsync(long sourceTaxonomyId, long parentSourceTaxonomyNodeId, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
