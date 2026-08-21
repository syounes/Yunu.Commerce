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
/// deploy/databases/sqlserver/008-add-segment-assignment-scope.sql,
/// deploy/databases/sqlserver/009-reset-canonical-taxonomy-starter.sql and
/// deploy/databases/sqlserver/012-add-canonical-taxonomy-concurrency-guard.sql
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
        await RunScriptAsync(connectionString, "012-add-canonical-taxonomy-concurrency-guard.sql");

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

    /// <summary>
    /// Test-only helper that persists <paramref name="child"/> through the
    /// only legitimate child-creation path, <see cref="ICanonicalTaxonomyRepository.AddChildAsync"/>,
    /// reloading <paramref name="parentId"/>'s current Revision immediately
    /// before the call so callers never reuse a stale Revision when creating
    /// multiple children under the same parent.
    /// </summary>
    private async Task<CanonicalTaxonomyNodeId> AddChildForTestAsync(CanonicalTaxonomyNode child, CanonicalTaxonomyNodeId parentId)
    {
        var (_, parentRevision) = (await _repository.GetWithRevisionAsync(parentId, CancellationToken.None))!.Value;
        var result = await _repository.AddChildAsync(child, parentRevision, CancellationToken.None);
        Assert.Equal(AddCanonicalTaxonomyChildOutcome.Created, result.Outcome);
        return result.AssignedId!.Value;
    }

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
        var childId1 = await AddChildForTestAsync(child1, parentId);

        var child2 = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "child-2", "Child 2", "child 2", null, 1, "Name parent-1 > Child 2",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId2 = await AddChildForTestAsync(child2, parentId);

        var otherChild = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), otherParentId, "other-child", "Other Child", "other child", null, 1, "Name other-parent-1 > Other Child",
            status: CanonicalTaxonomyNodeStatus.Active);
        await AddChildForTestAsync(otherChild, otherParentId);

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
        await AddChildForTestAsync(child, parentId);

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

        var loaded = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (persisted, revision) = loaded!.Value;
        persisted.Update("Updated Name", "updated name", "Updated description", "Updated Name");

        var updated = await _repository.UpdateAsync(persisted, revision, CancellationToken.None);
        Assert.True(updated);

        var reloaded = await _repository.GetByIdAsync(id, CancellationToken.None);

        Assert.Equal("Updated Name", reloaded!.Name);
        Assert.Equal("updated name", reloaded.NormalizedName);
        Assert.Equal("Updated description", reloaded.Description);
        Assert.Equal("Updated Name", reloaded.Path);
    }

    [Fact]
    public async Task UpdateAsync_Should_Persist_Status_Transition()
    {
        var node = CreateRootNode("archive-target");
        var id = await _repository.AddAsync(node, CancellationToken.None);

        var loaded = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (persisted, revision) = loaded!.Value;
        persisted.TransitionTo(CanonicalTaxonomyNodeStatus.Archived);

        var updated = await _repository.UpdateAsync(persisted, revision, CancellationToken.None);
        Assert.True(updated);

        var reloaded = await _repository.GetByIdAsync(id, CancellationToken.None);

        Assert.Equal(CanonicalTaxonomyNodeStatus.Archived, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateAsync_Should_Advance_Persisted_Revision_On_Success()
    {
        var node = CreateRootNode("revision-advance-target");
        var id = await _repository.AddAsync(node, CancellationToken.None);

        var loaded = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (persisted, revision) = loaded!.Value;
        Assert.Equal(1, revision);

        persisted.Update("Revision Advance Renamed", "revision advance renamed", null, "Revision Advance Renamed");
        var updated = await _repository.UpdateAsync(persisted, revision, CancellationToken.None);
        Assert.True(updated);

        var reloaded = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        Assert.Equal(revision + 1, reloaded!.Value.Revision);
    }

    [Fact]
    public async Task UpdateAsync_Stale_Writer_Should_Fail_And_Not_Overwrite_Winner()
    {
        var node = CreateRootNode("stale-writer-target");
        var id = await _repository.AddAsync(node, CancellationToken.None);

        // Writer A and Writer B both load the same persisted Revision.
        var loadedA = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (nodeA, revisionA) = loadedA!.Value;

        var loadedB = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (nodeB, revisionB) = loadedB!.Value;

        // Writer A updates successfully first.
        nodeA.Update("Winner Name", "winner name", "Winner description", "Winner Name");
        var updatedA = await _repository.UpdateAsync(nodeA, revisionA, CancellationToken.None);
        Assert.True(updatedA);

        // Writer B attempts to update using its now-stale Revision.
        nodeB.Update("Stale Name", "stale name", "Stale description", "Stale Name");
        var updatedB = await _repository.UpdateAsync(nodeB, revisionB, CancellationToken.None);
        Assert.False(updatedB);

        // Writer A's persisted state remains intact.
        var reloaded = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.Equal("Winner Name", reloaded!.Name);
        Assert.Equal("Winner description", reloaded.Description);
    }

    [Fact]
    public async Task UpdateAsync_Lifecycle_Stale_Writer_Should_Not_Overwrite_Winner()
    {
        var node = CreateRootNode("lifecycle-stale-writer-target");
        node.TransitionTo(CanonicalTaxonomyNodeStatus.Active);
        var id = await _repository.AddAsync(node, CancellationToken.None);

        var loadedA = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (nodeA, revisionA) = loadedA!.Value;

        var loadedB = await _repository.GetWithRevisionAsync(id, CancellationToken.None);
        var (nodeB, revisionB) = loadedB!.Value;

        // Writer A transitions to Inactive and commits first.
        nodeA.TransitionTo(CanonicalTaxonomyNodeStatus.Inactive);
        var updatedA = await _repository.UpdateAsync(nodeA, revisionA, CancellationToken.None);
        Assert.True(updatedA);

        // Writer B attempts to transition to Archived using the stale Revision.
        nodeB.TransitionTo(CanonicalTaxonomyNodeStatus.Archived);
        var updatedB = await _repository.UpdateAsync(nodeB, revisionB, CancellationToken.None);
        Assert.False(updatedB);

        var reloaded = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.Equal(CanonicalTaxonomyNodeStatus.Inactive, reloaded!.Status);
    }

    [Fact]
    public async Task AddChildAsync_Should_Fail_When_Parent_Was_Concurrently_Archived()
    {
        var parent = CreateRootNode("archive-vs-createchild-parent");
        parent.TransitionTo(CanonicalTaxonomyNodeStatus.Active);
        var parentId = await _repository.AddAsync(parent, CancellationToken.None);

        var loadedForArchive = await _repository.GetWithRevisionAsync(parentId, CancellationToken.None);
        var (parentForArchive, parentRevisionForArchive) = loadedForArchive!.Value;

        var loadedForCreateChild = await _repository.GetWithRevisionAsync(parentId, CancellationToken.None);
        var (_, parentRevisionForCreateChild) = loadedForCreateChild!.Value;

        // Archive wins the race first, incrementing the parent's Revision.
        parentForArchive.TransitionTo(CanonicalTaxonomyNodeStatus.Archived);
        var archived = await _repository.UpdateAsync(parentForArchive, parentRevisionForArchive, CancellationToken.None);
        Assert.True(archived);

        // CreateChild now races against a stale parent Revision and must fail,
        // proving an Archived parent + newly-created child can never commit.
        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "archive-vs-createchild-child", "Child", "child",
            null, 1, "Name archive-vs-createchild-parent > Child", status: CanonicalTaxonomyNodeStatus.Active);

        var result = await _repository.AddChildAsync(child, parentRevisionForCreateChild, CancellationToken.None);

        Assert.Equal(AddCanonicalTaxonomyChildOutcome.ParentConcurrencyConflict, result.Outcome);

        var hasChildren = await _repository.HasChildrenAsync(parentId, CancellationToken.None);
        Assert.False(hasChildren);
    }

    [Fact]
    public async Task AddChildAsync_Should_Succeed_And_Advance_Parent_Revision_When_Parent_Not_Archived()
    {
        var parent = CreateRootNode("createchild-advances-parent-revision");
        parent.TransitionTo(CanonicalTaxonomyNodeStatus.Active);
        var parentId = await _repository.AddAsync(parent, CancellationToken.None);

        var loaded = await _repository.GetWithRevisionAsync(parentId, CancellationToken.None);
        var (_, parentRevision) = loaded!.Value;

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "createchild-advances-parent-revision-child", "Child", "child",
            null, 1, "Name createchild-advances-parent-revision > Child", status: CanonicalTaxonomyNodeStatus.Active);

        var result = await _repository.AddChildAsync(child, parentRevision, CancellationToken.None);
        Assert.Equal(AddCanonicalTaxonomyChildOutcome.Created, result.Outcome);

        var reloadedParent = await _repository.GetWithRevisionAsync(parentId, CancellationToken.None);
        Assert.Equal(parentRevision + 1, reloadedParent!.Value.Revision);

        var hasChildren = await _repository.HasChildrenAsync(parentId, CancellationToken.None);
        Assert.True(hasChildren);
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
        var childId = await AddChildForTestAsync(child, parentId);

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
        var apparelId = await AddChildForTestAsync(apparel, rootId);

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
        var shoesId = await AddChildForTestAsync(shoes, apparelId);

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

    [Fact]
    public async Task GetRootsAsync_Should_Return_Root_Node_And_Not_Its_Children()
    {
        var root = CreateRootNode("get-roots-parent");
        var rootId = await _repository.AddAsync(root, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), rootId, "get-roots-child", "Child", "child", null, 1, "Name get-roots-parent > Child",
            status: CanonicalTaxonomyNodeStatus.Active);
        var childId = await AddChildForTestAsync(child, rootId);

        var roots = await _repository.GetRootsAsync(CancellationToken.None);

        Assert.Contains(roots, r => r.Id == rootId);
        Assert.DoesNotContain(roots, r => r.Id == childId);
        Assert.All(roots, r => Assert.Null(r.ParentId));
    }

    [Fact]
    public async Task AddAsync_With_Root_Node_Should_Still_Succeed()
    {
        var node = CreateRootNode("addasync-root-guard");

        var id = await _repository.AddAsync(node, CancellationToken.None);

        var persisted = await _repository.GetByIdAsync(id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Null(persisted!.ParentId);
    }

    [Fact]
    public async Task AddAsync_With_Child_Node_Should_Be_Rejected_And_Not_Persist()
    {
        var parent = CreateRootNode("addasync-child-guard-parent");
        var parentId = await _repository.AddAsync(parent, CancellationToken.None);

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "addasync-child-guard-child", "Child", "child",
            null, 1, "Name addasync-child-guard-parent > Child", status: CanonicalTaxonomyNodeStatus.Active);

        await Assert.ThrowsAsync<ArgumentException>(() => _repository.AddAsync(child, CancellationToken.None));

        var hasChildren = await _repository.HasChildrenAsync(parentId, CancellationToken.None);
        Assert.False(hasChildren);
    }

    [Fact]
    public async Task AddAsync_Continues_To_Allow_Multiple_Independent_Root_Nodes()
    {
        var root1 = await _repository.AddAsync(CreateRootNode("multi-root-1"), CancellationToken.None);
        var root2 = await _repository.AddAsync(CreateRootNode("multi-root-2"), CancellationToken.None);
        var root3 = await _repository.AddAsync(CreateRootNode("multi-root-3"), CancellationToken.None);

        var roots = await _repository.GetRootsAsync(CancellationToken.None);

        Assert.Contains(roots, r => r.Id == root1);
        Assert.Contains(roots, r => r.Id == root2);
        Assert.Contains(roots, r => r.Id == root3);
    }

    [Fact]
    public async Task Multiple_Children_Under_Same_Parent_Succeed_When_Each_Uses_Latest_Parent_Revision()
    {
        var parentId = await _repository.AddAsync(CreateRootNode("multi-child-parent"), CancellationToken.None);

        var child1 = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "multi-child-1", "Child 1", "child 1", null, 1, "Name multi-child-parent > Child 1",
            status: CanonicalTaxonomyNodeStatus.Active);
        await AddChildForTestAsync(child1, parentId);

        // Reload the parent's current Revision (already advanced by the
        // first AddChildAsync) rather than reusing a stale value.
        var child2 = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "multi-child-2", "Child 2", "child 2", null, 1, "Name multi-child-parent > Child 2",
            status: CanonicalTaxonomyNodeStatus.Active);
        await AddChildForTestAsync(child2, parentId);

        var children = await _repository.GetChildrenAsync(parentId, CancellationToken.None);
        Assert.Equal(2, children.Count);
    }

    [Fact]
    public async Task AddChildAsync_With_Stale_Parent_Revision_Fails_With_Concurrency_Conflict()
    {
        var parentId = await _repository.AddAsync(CreateRootNode("stale-revision-parent"), CancellationToken.None);

        var loaded = await _repository.GetWithRevisionAsync(parentId, CancellationToken.None);
        var (_, staleRevision) = loaded!.Value;

        var firstChild = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "stale-revision-first-child", "Child 1", "child 1",
            null, 1, "Name stale-revision-parent > Child 1", status: CanonicalTaxonomyNodeStatus.Active);
        var firstResult = await _repository.AddChildAsync(firstChild, staleRevision, CancellationToken.None);
        Assert.Equal(AddCanonicalTaxonomyChildOutcome.Created, firstResult.Outcome);

        var secondChild = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, "stale-revision-second-child", "Child 2", "child 2",
            null, 1, "Name stale-revision-parent > Child 2", status: CanonicalTaxonomyNodeStatus.Active);
        var secondResult = await _repository.AddChildAsync(secondChild, staleRevision, CancellationToken.None);

        Assert.Equal(AddCanonicalTaxonomyChildOutcome.ParentConcurrencyConflict, secondResult.Outcome);
    }
}
