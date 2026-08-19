using Xunit;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyRoots;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy;

public class GetCanonicalTaxonomyRootsHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Only_Nodes_With_Null_ParentId()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(1), "cat", "Catálogo", "catalogo", null, "Catálogo",
            googleCategoryId: null, source: CanonicalTaxonomySource.Yunu, status: CanonicalTaxonomyNodeStatus.Active);
        await repo.AddAsync(root, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(2), new CanonicalTaxonomyNodeId(1), "child", "Child", "child",
            null, 1, "Catálogo > Child", status: CanonicalTaxonomyNodeStatus.Active);
        await repo.AddAsync(child, CancellationToken.None);

        var handler = new GetCanonicalTaxonomyRootsHandler(repo);
        var result = await handler.HandleAsync(new GetCanonicalTaxonomyRootsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(1, result.First().CanonicalTaxonomyNodeId);
    }

    [Fact]
    public async Task Handle_Should_Not_Return_Children()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(1), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        await repo.AddAsync(root, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(2), new CanonicalTaxonomyNodeId(1), "child", "Child", "child",
            null, 1, "Catálogo > Child", status: CanonicalTaxonomyNodeStatus.Active);
        await repo.AddAsync(child, CancellationToken.None);

        var handler = new GetCanonicalTaxonomyRootsHandler(repo);
        var result = await handler.HandleAsync(new GetCanonicalTaxonomyRootsQuery(), CancellationToken.None);

        Assert.DoesNotContain(result, r => r.CanonicalTaxonomyNodeId == 2);
    }

    [Fact]
    public async Task Handle_Should_Map_GoogleCategoryId_Path_Source_And_Status()
    {
        var repo = new FakeCanonicalTaxonomyRepository();

        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(1), "cat", "Catálogo", "catalogo", null, "Catálogo",
            googleCategoryId: 123, source: CanonicalTaxonomySource.Google, status: CanonicalTaxonomyNodeStatus.Active);
        await repo.AddAsync(root, CancellationToken.None);

        var handler = new GetCanonicalTaxonomyRootsHandler(repo);
        var result = await handler.HandleAsync(new GetCanonicalTaxonomyRootsQuery(), CancellationToken.None);

        var response = Assert.Single(result);
        Assert.Equal(123, response.GoogleCategoryId);
        Assert.Equal("Catálogo", response.Path);
        Assert.Equal("Google", response.Source);
        Assert.Equal("Active", response.Status);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Collection_When_No_Roots_Exist()
    {
        var repo = new FakeCanonicalTaxonomyRepository();
        var handler = new GetCanonicalTaxonomyRootsHandler(repo);

        var result = await handler.HandleAsync(new GetCanonicalTaxonomyRootsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
