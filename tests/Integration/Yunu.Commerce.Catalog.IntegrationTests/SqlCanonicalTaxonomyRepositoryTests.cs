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
/// deploy/databases/sqlserver/001-google-taxonomy-tables.sql,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql,
/// deploy/databases/sqlserver/007-drop-legacy-catalog-hierarchy.sql,
/// deploy/databases/sqlserver/008-add-segment-assignment-scope.sql and
/// deploy/databases/sqlserver/009-reset-canonical-taxonomy-starter.sql
/// directly against the container. Migration 009 requires Google categories
/// 166 ("Vestuário e acessórios") and 187 ("Sapatos") to already exist in
/// Catalog.GoogleTaxonomyCategories with the expected pt-BR names/paths, so
/// this fixture seeds them minimally before running it.
/// </summary>
public sealed class SqlCanonicalTaxonomyRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private SqlCanonicalTaxonomyRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync(connectionString, "001-google-taxonomy-tables.sql");
        await RunScriptAsync(connectionString, "006-create-canonical-taxonomy-segmentation.sql");
        await RunScriptAsync(connectionString, "007-drop-legacy-catalog-hierarchy.sql");
        await RunScriptAsync(connectionString, "008-add-segment-assignment-scope.sql");
        await SeedGoogleTaxonomyCategoriesAsync(connectionString);
        await RunScriptAsync(connectionString, "009-reset-canonical-taxonomy-starter.sql");

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

    private static async Task SeedGoogleTaxonomyCategoriesAsync(string connectionString)
    {
        const string sql = """
            INSERT INTO Catalog.GoogleTaxonomyCategories
                (GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage, CreatedAt, ImportedAt)
            VALUES
                (166, NULL, N'Vestuário e acessórios', N'Vestuário e acessórios', 1, 0, 1, N'pt-BR', SYSUTCDATETIME(), SYSUTCDATETIME()),
                (187, 166, N'Sapatos', N'Vestuário e acessórios > Sapatos', 2, 1, 1, N'pt-BR', SYSUTCDATETIME(), SYSUTCDATETIME()),
                (999001, NULL, N'Categoria de Teste', N'Categoria de Teste', 1, 1, 1, N'pt-BR', SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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
            $"Name {code}",
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
            new CanonicalTaxonomyNodeId(0), parentId, "child-1", "Child 1", "child 1", null, 1, "Name parent-1 > Child 1",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId1 = await _repository.AddAsync(child1, CancellationToken.None);

        var child2 = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "child-2", "Child 2", "child 2", null, 1, "Name parent-1 > Child 2",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId2 = await _repository.AddAsync(child2, CancellationToken.None);

        var otherChild = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), otherParentId, "other-child", "Other Child", "other child", null, 1, "Name other-parent-1 > Other Child",
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
            new CanonicalTaxonomyNodeId(0), parentId, "has-children-child", "Child", "child", null, 1, "Name has-children-parent > Child",
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

    [Fact]
    public async Task CreateRoot_Should_Produce_Path_Using_Only_The_Name()
    {
        var node = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            "catalog-path-root",
            "Catálogo Teste Root",
            "catalogo teste root",
            null,
            "Catálogo Teste Root",
            status: CanonicalTaxonomyNodeStatus.Active);

        var id = await _repository.AddAsync(node, CancellationToken.None);
        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);

        Assert.Equal("Catálogo Teste Root", persisted!.Path);
        Assert.Equal(0, persisted.Depth);
    }

    [Fact]
    public async Task CreateChild_Should_Build_Path_From_Parent_Path_And_Name_Separated_By_GreaterThan()
    {
        var parent = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            "catalog-path-parent",
            "Catálogo Teste Pai",
            "catalogo teste pai",
            null,
            "Catálogo Teste Pai",
            status: CanonicalTaxonomyNodeStatus.Active);
        var parentId = await _repository.AddAsync(parent, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0),
            parentId,
            "apparel-path-child",
            "Vestuário e acessórios",
            "vestuario e acessorios",
            null,
            1,
            "Catálogo Teste Pai > Vestuário e acessórios",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId = await _repository.AddAsync(child, CancellationToken.None);

        var persistedChild = await _repository.GetByIdAsync(childId, CancellationToken.None);

        Assert.Equal("Catálogo Teste Pai > Vestuário e acessórios", persistedChild!.Path);
        Assert.Equal(1, persistedChild.Depth);
    }

    [Fact]
    public async Task CreateGrandchild_Should_Preserve_Hierarchy_With_GreaterThan_Separators()
    {
        var root = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            "catalog-path-grandparent",
            "Catálogo Teste Avô",
            "catalogo teste avo",
            null,
            "Catálogo Teste Avô",
            status: CanonicalTaxonomyNodeStatus.Active);
        var rootId = await _repository.AddAsync(root, CancellationToken.None);

        var apparel = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0),
            rootId,
            "apparel-path-parent",
            "Vestuário e acessórios",
            "vestuario e acessorios",
            null,
            1,
            "Catálogo Teste Avô > Vestuário e acessórios",
            status: CanonicalTaxonomyNodeStatus.Active);
        var apparelId = await _repository.AddAsync(apparel, CancellationToken.None);

        var shoes = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0),
            apparelId,
            "shoes-path-child",
            "Sapatos",
            "sapatos",
            null,
            2,
            "Catálogo Teste Avô > Vestuário e acessórios > Sapatos",
            status: CanonicalTaxonomyNodeStatus.Active);
        var shoesId = await _repository.AddAsync(shoes, CancellationToken.None);

        var persistedShoes = await _repository.GetByIdAsync(shoesId, CancellationToken.None);

        Assert.Equal("Catálogo Teste Avô > Vestuário e acessórios > Sapatos", persistedShoes!.Path);
        Assert.Equal(2, persistedShoes.Depth);
    }

    [Fact]
    public void CreateRoot_With_Source_Google_And_No_GoogleCategoryId_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() =>
            CanonicalTaxonomyNode.CreateRoot(
                new CanonicalTaxonomyNodeId(0),
                "google-without-category",
                "Categoria Google",
                "categoria google",
                null,
                "Categoria Google",
                googleCategoryId: null,
                source: CanonicalTaxonomySource.Google,
                status: CanonicalTaxonomyNodeStatus.Active));
    }

    [Fact]
    public async Task GoogleCategoryId_Should_Be_Persisted_And_Rehydrated()
    {
        var node = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            "google-category-root",
            "Categoria de Teste",
            "categoria de teste",
            null,
            "Categoria de Teste",
            googleCategoryId: 999001,
            source: CanonicalTaxonomySource.Google,
            status: CanonicalTaxonomyNodeStatus.Active);

        var id = await _repository.AddAsync(node, CancellationToken.None);
        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);

        Assert.Equal(999001, persisted!.GoogleCategoryId);
        Assert.Equal(CanonicalTaxonomySource.Google, persisted.Source);
    }
}
