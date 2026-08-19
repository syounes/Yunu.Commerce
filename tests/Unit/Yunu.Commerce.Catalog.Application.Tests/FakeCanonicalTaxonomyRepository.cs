using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for ICanonicalTaxonomyRepository.
/// </summary>
internal sealed class FakeCanonicalTaxonomyRepository : ICanonicalTaxonomyRepository
{
    private readonly Dictionary<long, CanonicalTaxonomyNode> _nodes = new();
    private readonly Dictionary<long, List<long>> _children = new();
    private long _nextId = 1;

    public Task<CanonicalTaxonomyNodeId> AddAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken)
    {
        var id = node.Id.Value == 0 ? _nextId++ : node.Id.Value;
        _nodes[id] = node;
        return Task.FromResult(new CanonicalTaxonomyNodeId(id));
    }

    public Task UpdateAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken)
    {
        _nodes[node.Id.Value] = node;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        _nodes.Remove(id.Value);
        return Task.CompletedTask;
    }

    public Task<CanonicalTaxonomyNode?> GetByIdAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        _nodes.TryGetValue(id.Value, out var node);
        return Task.FromResult(node);
    }

    public Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetChildrenAsync(CanonicalTaxonomyNodeId parentId, CancellationToken cancellationToken)
    {
        if (!_children.TryGetValue(parentId.Value, out var childIds))
        {
            return Task.FromResult<IReadOnlyCollection<CanonicalTaxonomyNode>>(Array.Empty<CanonicalTaxonomyNode>());
        }

        var children = childIds.Select(id => _nodes[id]).ToList();
        return Task.FromResult<IReadOnlyCollection<CanonicalTaxonomyNode>>(children);
    }

    public Task<bool> HasChildrenAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        var hasChildren = _children.TryGetValue(id.Value, out var childIds) && childIds.Count > 0;
        return Task.FromResult(hasChildren);
    }

    public void Add(long id, CanonicalTaxonomyNode node)
    {
        _nodes[id] = node;
    }
}
