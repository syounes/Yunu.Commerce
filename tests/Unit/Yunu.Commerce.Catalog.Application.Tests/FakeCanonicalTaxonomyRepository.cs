using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests;

/// <summary>
/// Test-only fake for ICanonicalTaxonomyRepository. Derives parent-child
/// relationships from <see cref="CanonicalTaxonomyNode.ParentId"/> so that
/// <see cref="HasChildrenAsync"/> correctly reflects nodes added via
/// <see cref="Add"/> or <see cref="AddAsync"/>. Models the same
/// first-writer-wins Revision semantics as the real SQL Server repository
/// (docs task: "Yunu.Commerce - Canonical Taxonomy Concurrency Guard"), so
/// Application-layer tests can exercise stale-writer scenarios without a
/// real database.
/// </summary>
internal sealed class FakeCanonicalTaxonomyRepository : ICanonicalTaxonomyRepository
{
    private readonly Dictionary<long, CanonicalTaxonomyNode> _nodes = new();
    private readonly Dictionary<long, long> _revisions = new();
    private readonly Dictionary<long, List<long>> _children = new();
    private long _nextId = 1;

    /// <summary>
    /// Test-only hook invoked exactly once, immediately after
    /// <see cref="GetWithRevisionAsync"/> returns, to simulate a concurrent
    /// writer committing a change in the window between this caller's read
    /// and its later conditional write (docs task: "Yunu.Commerce -
    /// Canonical Taxonomy Concurrency Guard"). Cleared after firing.
    /// </summary>
    public Action? SimulateConcurrentWriteAfterNextRead { get; set; }

    public Task<CanonicalTaxonomyNodeId> AddAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken)
    {
        var id = node.Id.Value == 0 ? _nextId++ : node.Id.Value;
        var stored = Rehydrate(id, node);
        _nodes[id] = stored;
        _revisions[id] = 1;
        RegisterParentRelationship(id, stored);
        return Task.FromResult(new CanonicalTaxonomyNodeId(id));
    }

    public Task<AddCanonicalTaxonomyChildResult> AddChildAsync(
        CanonicalTaxonomyNode node,
        long expectedParentRevision,
        CancellationToken cancellationToken)
    {
        if (node.ParentId is not { } parentId)
        {
            throw new ArgumentException("AddChildAsync requires a node with a ParentId.", nameof(node));
        }

        if (!_nodes.TryGetValue(parentId.Value, out var parent))
        {
            return Task.FromResult(new AddCanonicalTaxonomyChildResult { Outcome = AddCanonicalTaxonomyChildOutcome.ParentNotFound });
        }

        if (_revisions[parentId.Value] != expectedParentRevision)
        {
            // Stale writers always fail with a concurrency conflict,
            // regardless of what the concurrent change happened to be
            // (matches SqlCanonicalTaxonomyRepository.AddChildAsync).
            return Task.FromResult(new AddCanonicalTaxonomyChildResult { Outcome = AddCanonicalTaxonomyChildOutcome.ParentConcurrencyConflict });
        }

        if (parent.Status == CanonicalTaxonomyNodeStatus.Archived)
        {
            return Task.FromResult(new AddCanonicalTaxonomyChildResult { Outcome = AddCanonicalTaxonomyChildOutcome.ParentArchived });
        }

        _revisions[parentId.Value]++;

        var id = node.Id.Value == 0 ? _nextId++ : node.Id.Value;
        var stored = Rehydrate(id, node);
        _nodes[id] = stored;
        _revisions[id] = 1;
        RegisterParentRelationship(id, stored);

        return Task.FromResult(new AddCanonicalTaxonomyChildResult
        {
            Outcome = AddCanonicalTaxonomyChildOutcome.Created,
            AssignedId = new CanonicalTaxonomyNodeId(id)
        });
    }

    public Task<bool> UpdateAsync(CanonicalTaxonomyNode node, long expectedRevision, CancellationToken cancellationToken)
    {
        if (!_revisions.TryGetValue(node.Id.Value, out var currentRevision) || currentRevision != expectedRevision)
        {
            return Task.FromResult(false);
        }

        _nodes[node.Id.Value] = node;
        _revisions[node.Id.Value] = currentRevision + 1;
        return Task.FromResult(true);
    }

    public Task<CanonicalTaxonomyNode?> GetByIdAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        _nodes.TryGetValue(id.Value, out var node);
        return Task.FromResult(node is null ? null : Rehydrate(id.Value, node));
    }

    public Task<(CanonicalTaxonomyNode Node, long Revision)?> GetWithRevisionAsync(
        CanonicalTaxonomyNodeId id,
        CancellationToken cancellationToken)
    {
        if (!_nodes.TryGetValue(id.Value, out var node))
        {
            return Task.FromResult<(CanonicalTaxonomyNode Node, long Revision)?>(null);
        }

        var revision = _revisions[id.Value];
        var copy = Rehydrate(id.Value, node);

        var hook = SimulateConcurrentWriteAfterNextRead;
        if (hook is not null)
        {
            SimulateConcurrentWriteAfterNextRead = null;
            hook();
        }

        return Task.FromResult<(CanonicalTaxonomyNode Node, long Revision)?>((copy, revision));
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

    public Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetRootsAsync(CancellationToken cancellationToken)
    {
        var roots = _nodes.Values.Where(n => n.ParentId is null).OrderBy(n => n.Path).ToList();
        return Task.FromResult<IReadOnlyCollection<CanonicalTaxonomyNode>>(roots);
    }

    public void Add(long id, CanonicalTaxonomyNode node)
    {
        _nodes[id] = node;
        _revisions[id] = 1;
        RegisterParentRelationship(id, node);
    }

    /// <summary>
    /// Test-only helper to simulate a concurrent writer that already
    /// committed a change, advancing the persisted Revision without going
    /// through the normal Update/AddChild path (docs task: "Yunu.Commerce -
    /// Canonical Taxonomy Concurrency Guard").
    /// </summary>
    public void BumpRevisionForTest(CanonicalTaxonomyNodeId id)
    {
        _revisions[id.Value]++;
    }

    public long GetRevisionForTest(CanonicalTaxonomyNodeId id) => _revisions[id.Value];

    /// <summary>
    /// Test-only helper to simulate an inconsistent tree by removing a node
    /// while descendants still reference it as ParentId. There is no
    /// production DeleteAsync (docs task: "Yunu.Commerce V9 - Canonical
    /// Taxonomy Lifecycle + Usage Guards").
    /// </summary>
    public void RemoveForTest(CanonicalTaxonomyNodeId id)
    {
        _nodes.Remove(id.Value);
    }

    private static CanonicalTaxonomyNode Rehydrate(long id, CanonicalTaxonomyNode node)
    {
        return CanonicalTaxonomyNode.Hydrate(
            new CanonicalTaxonomyNodeId(id),
            node.ParentId,
            node.GoogleCategoryId,
            node.Code,
            node.Name,
            node.NormalizedName,
            node.Description,
            node.Depth,
            node.Path,
            node.Source,
            node.Status);
    }

    private void RegisterParentRelationship(long id, CanonicalTaxonomyNode node)
    {
        if (node.ParentId is not { } parentId)
        {
            return;
        }

        if (!_children.TryGetValue(parentId.Value, out var childIds))
        {
            childIds = new List<long>();
            _children[parentId.Value] = childIds;
        }

        if (!childIds.Contains(id))
        {
            childIds.Add(id);
        }
    }
}
