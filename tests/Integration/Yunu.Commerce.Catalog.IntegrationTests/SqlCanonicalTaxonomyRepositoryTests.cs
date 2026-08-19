using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for SqlCanonicalTaxonomyRepository against a real SQL
/// Server instance via Testcontainers (docs task: "Canonical Taxonomy +
/// Segments Domain" §19-§22). The schema is created by executing
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql
/// directly against the container.
/// </summary>
public sealed class SqlCanonicalTaxonomyRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private SqlCanonicalTaxonomyRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync(connectionString, "006-create-canonical-taxonomy-segmentation.sql");

        var options = Options.Create(new GoogleTaxonomySqlOptions
        {
            ConnectionString = connectionString
        });

        _repository = new SqlCanonicalTaxonomyRepository(options);
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    private static async Task RunScriptAsync(string connectionString, string fileName)
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "deploy", "databases", "sqlserver", fileName);

        var script = await File.ReadAllTextAsync(Path.GetFullPath(scriptPath));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var batch in script.Split("GO", StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static CanonicalTaxonomyNode CreateRootNode(string code) =>
        CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            code,
            $"Name {code}",
            $"name {code}",
            "Description",
            $"/{code}",
            status: CanonicalTaxonomyNodeStatus.Active);

    [Fact]
    public async Task AddAsync_Should_Insert_Node_And_Return_Generated_Id()
    {
        var node = CreateRootNode("root-1");

        var id = await _repository.AddAsync(node, CancellationToken.None);

        Assert.True(id.Value > 0);

        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("root-1", persisted!.Code);
        Assert.Equal(CanonicalTaxonomyNodeStatus.Active, persisted.Status);
    }

    [Fact]
    public async Task GetByIdAsync_For_Unknown_Id_Should_Return_Null()
    {
        var result = await _repository.GetByIdAsync(new CanonicalTaxonomyNodeId(999_999), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetChildrenAsync_Should_Return_Only_Direct_Children()
    {
        var parentNode = CreateRootNode("parent-1");
        var parentId = await _repository.AddAsync(parentNode, CancellationToken.None);

        var otherParentNode = CreateRootNode("other-parent-1");
        var otherParentId = await _repository.AddAsync(otherParentNode, CancellationToken.None);

        var child1 = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "child-1", "Child 1", "child 1", null, 1, "/parent-1/child-1",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId1 = await _repository.AddAsync(child1, CancellationToken.None);

        var child2 = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "child-2", "Child 2", "child 2", null, 1, "/parent-1/child-2",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId2 = await _repository.AddAsync(child2, CancellationToken.None);

        var otherChild = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), otherParentId, "other-child", "Other Child", "other child", null, 1, "/other-parent-1/other-child",
            status: CanonicalTaxonomyNodeStatus.Active);
        await _repository.AddAsync(otherChild, CancellationToken.None);

        var children = await _repository.GetChildrenAsync(parentId, CancellationToken.None);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, c => c.Id == childId1);
        Assert.Contains(children, c => c.Id == childId2);
    }

    [Fact]
    public async Task HasChildrenAsync_Should_Return_True_When_Children_Exist()
    {
        var parentNode = CreateRootNode("has-children-parent");
        var parentId = await _repository.AddAsync(parentNode, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "has-children-child", "Child", "child", null, 1, "/has-children-parent/has-children-child",
            status: CanonicalTaxonomyNodeStatus.Active);
        await _repository.AddAsync(child, CancellationToken.None);

        var hasChildren = await _repository.HasChildrenAsync(parentId, CancellationToken.None);

        Assert.True(hasChildren);
    }

    [Fact]
    public async Task HasChildrenAsync_Should_Return_False_When_No_Children_Exist()
    {
        var node = CreateRootNode("leaf-node");
        var id = await _repository.AddAsync(node, CancellationToken.None);

        var hasChildren = await _repository.HasChildrenAsync(id, CancellationToken.None);

        Assert.False(hasChildren);
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Allowed_Changes()
    {
        var node = CreateRootNode("update-target");
        var id = await _repository.AddAsync(node, CancellationToken.None);

        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        persisted!.Update("Updated Name", "updated name", "Updated description");

        await _repository.UpdateAsync(persisted, CancellationToken.None);

        var reloaded = await _repository.GetByIdAsync(id, CancellationToken.None);

        Assert.Equal("Updated Name", reloaded!.Name);
        Assert.Equal("updated name", reloaded.NormalizedName);
        Assert.Equal("Updated description", reloaded.Description);
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Node()
    {
        var node = CreateRootNode("delete-target");
        var id = await _repository.AddAsync(node, CancellationToken.None);

        await _repository.DeleteAsync(id, CancellationToken.None);

        var reloaded = await _repository.GetByIdAsync(id, CancellationToken.None);

        Assert.Null(reloaded);
    }
}
