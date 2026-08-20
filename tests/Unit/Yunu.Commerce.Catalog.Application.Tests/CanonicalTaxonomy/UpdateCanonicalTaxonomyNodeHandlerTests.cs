using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.UpdateCanonicalTaxonomyNode;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Xunit;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy;

public class UpdateCanonicalTaxonomyNodeHandlerTests
{
    [Fact]
    public async Task Update_root_leaf_node_recomputes_path_from_new_name()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var productRepo = new FakeProductRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var handler = new UpdateCanonicalTaxonomyNodeHandler(repo, productRepo, new FakeCanonicalTaxonomyNodeUsageReader());
        var command = new UpdateCanonicalTaxonomyNodeCommand
        {
            CanonicalTaxonomyNodeId = rootId.Value,
            Name = "  Catálogo principal  ",
            Description = null
        };

        await handler.HandleAsync(command, CancellationToken.None);

        var updated = await repo.GetByIdAsync(rootId, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Catálogo principal", updated!.Name);
        Assert.Equal("Catálogo principal", updated.Path);
    }

    [Fact]
    public async Task Update_child_leaf_node_recomputes_path_using_parent_path()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var productRepo = new FakeProductRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var vestuario = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), rootId, "vestuario", "Vestuário e acessórios", "vestuario e acessorios",
            null, 1, "Catálogo > Vestuário e acessórios", status: CanonicalTaxonomyNodeStatus.Active);
        var vestuarioId = await repo.AddAsync(vestuario, CancellationToken.None);

        var sapatos = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), vestuarioId, "sapatos", "Sapatos", "sapatos",
            null, 2, "Catálogo > Vestuário e acessórios > Sapatos", status: CanonicalTaxonomyNodeStatus.Active);
        var sapatosId = await repo.AddAsync(sapatos, CancellationToken.None);

        var handler = new UpdateCanonicalTaxonomyNodeHandler(repo, productRepo, new FakeCanonicalTaxonomyNodeUsageReader());
        var command = new UpdateCanonicalTaxonomyNodeCommand
        {
            CanonicalTaxonomyNodeId = sapatosId.Value,
            Name = "Calçados",
            Description = null
        };

        await handler.HandleAsync(command, CancellationToken.None);

        var updated = await repo.GetByIdAsync(sapatosId, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Calçados", updated!.Name);
        Assert.Equal("Catálogo > Vestuário e acessórios > Calçados", updated.Path);
        Assert.Equal(sapatos.Depth, updated.Depth);
        Assert.Equal(sapatos.ParentId, updated.ParentId);
        Assert.Equal(sapatos.Code, updated.Code);
        Assert.Equal(sapatos.GoogleCategoryId, updated.GoogleCategoryId);
        Assert.Equal(sapatos.Source, updated.Source);
        Assert.Equal(sapatos.Status, updated.Status);
    }

    [Fact]
    public async Task Update_node_with_children_throws()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var productRepo = new FakeProductRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), rootId, "child", "Child", "child",
            null, 1, "Catálogo > Child", status: CanonicalTaxonomyNodeStatus.Active);
        await repo.AddAsync(child, CancellationToken.None);

        var handler = new UpdateCanonicalTaxonomyNodeHandler(repo, productRepo, new FakeCanonicalTaxonomyNodeUsageReader());
        var command = new UpdateCanonicalTaxonomyNodeCommand
        {
            CanonicalTaxonomyNodeId = rootId.Value,
            Name = "Novo nome"
        };

        await Assert.ThrowsAsync<CanonicalTaxonomyNodeNotLeafException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_node_used_by_product_throws()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var productRepo = new FakeProductRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);
        productRepo.MarkCanonicalTaxonomyNodeInUse(rootId);

        var handler = new UpdateCanonicalTaxonomyNodeHandler(repo, productRepo, new FakeCanonicalTaxonomyNodeUsageReader());
        var command = new UpdateCanonicalTaxonomyNodeCommand
        {
            CanonicalTaxonomyNodeId = rootId.Value,
            Name = "Novo nome"
        };

        await Assert.ThrowsAsync<CanonicalTaxonomyNodeInUseException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_child_with_missing_parent_throws()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var productRepo = new FakeProductRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await repo.AddAsync(root, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), rootId, "child", "Child", "child",
            null, 1, "Catálogo > Child", status: CanonicalTaxonomyNodeStatus.Active);
        var childId = await repo.AddAsync(child, CancellationToken.None);

        // Simulate an inconsistent tree: remove the parent while the child still references it.
        repo.RemoveForTest(rootId);

        var handler = new UpdateCanonicalTaxonomyNodeHandler(repo, productRepo, new FakeCanonicalTaxonomyNodeUsageReader());
        var command = new UpdateCanonicalTaxonomyNodeCommand
        {
            CanonicalTaxonomyNodeId = childId.Value,
            Name = "Novo nome"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }
}
