using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.CreateCanonicalTaxonomyNode;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy;

/// <summary>
/// Regression coverage for <see cref="CreateCanonicalTaxonomyNodeHandler"/>,
/// including the Archive x CreateChild structural concurrency guard (docs
/// task: "Yunu.Commerce - Canonical Taxonomy Concurrency Guard" §7).
/// </summary>
public class CreateCanonicalTaxonomyNodeHandlerTests
{
    [Fact]
    public async Task Create_root_node_succeeds()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var handler = new CreateCanonicalTaxonomyNodeHandler(repo);

        var result = await handler.HandleAsync(
            new CreateCanonicalTaxonomyNodeCommand { Code = "root", Name = "Raiz" },
            CancellationToken.None);

        var persisted = await repo.GetByIdAsync(new CanonicalTaxonomyNodeId(result.CanonicalTaxonomyNodeId), CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Raiz", persisted!.Name);
    }

    [Fact]
    public async Task Create_child_under_archived_parent_throws()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Archived);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var handler = new CreateCanonicalTaxonomyNodeHandler(repo);

        var command = new CreateCanonicalTaxonomyNodeCommand
        {
            ParentId = rootId.Value,
            Code = "child",
            Name = "Filho"
        };

        await Assert.ThrowsAsync<CanonicalTaxonomyNodeParentArchivedException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Create_child_under_missing_parent_throws()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var handler = new CreateCanonicalTaxonomyNodeHandler(repo);

        var command = new CreateCanonicalTaxonomyNodeCommand
        {
            ParentId = 999_999,
            Code = "child",
            Name = "Filho"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Create_child_fails_when_parent_was_concurrently_archived()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var handler = new CreateCanonicalTaxonomyNodeHandler(repo);

        // Simulate a concurrent Archive committing (and bumping the parent's
        // Revision) in the window between CreateChild's read of the parent
        // and its later conditional write.
        repo.SimulateConcurrentWriteAfterNextRead = () => repo.BumpRevisionForTest(rootId);

        var command = new CreateCanonicalTaxonomyNodeCommand
        {
            ParentId = rootId.Value,
            Code = "child",
            Name = "Filho"
        };

        await Assert.ThrowsAsync<CanonicalTaxonomyNodeConcurrencyConflictException>(
            () => handler.HandleAsync(command, CancellationToken.None));

        var hasChildren = await repo.HasChildrenAsync(rootId, CancellationToken.None);
        Assert.False(hasChildren);
    }

    [Fact]
    public async Task Create_child_succeeds_and_advances_parent_revision()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var revisionBefore = repo.GetRevisionForTest(rootId);

        var handler = new CreateCanonicalTaxonomyNodeHandler(repo);
        var command = new CreateCanonicalTaxonomyNodeCommand
        {
            ParentId = rootId.Value,
            Code = "child",
            Name = "Filho"
        };

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(revisionBefore + 1, repo.GetRevisionForTest(rootId));

        var hasChildren = await repo.HasChildrenAsync(rootId, CancellationToken.None);
        Assert.True(hasChildren);
    }

    [Fact]
    public async Task AddAsync_With_Root_Node_Still_Succeeds()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);

        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var persisted = await repo.GetByIdAsync(rootId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.ParentId);
    }

    [Fact]
    public async Task AddAsync_With_Child_Node_Is_Rejected_And_Does_Not_Persist()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), rootId, "child", "Filho", "filho", null, 1, "Catálogo > Filho",
            status: CanonicalTaxonomyNodeStatus.Active);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.AddAsync(child, CancellationToken.None));

        var hasChildren = await repo.HasChildrenAsync(rootId, CancellationToken.None);
        Assert.False(hasChildren);
    }
}
