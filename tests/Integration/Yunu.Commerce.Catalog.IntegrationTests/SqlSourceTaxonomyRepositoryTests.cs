using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for SqlSourceTaxonomyRepository against a real SQL
/// Server instance via Testcontainers (docs/adr/0014-provider-neutral-source-taxonomy.md).
/// The schema is created by executing
/// deploy/databases/sqlserver/014-create-source-taxonomy-foundation.sql
/// directly against the container. The migration script is executed twice
/// in a single fixture instance to also prove it is safely rerunnable
/// (idempotent) without duplicating schema objects.
/// </summary>
public sealed class SqlSourceTaxonomyRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private SqlSourceTaxonomyRepository _repository = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        _connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync(_connectionString, "014-create-source-taxonomy-foundation.sql");

        var options = Options.Create(new GoogleTaxonomySqlOptions
        {
            ConnectionString = _connectionString
        });

        _repository = new SqlSourceTaxonomyRepository(options);
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

    private static SourceTaxonomyCreateRecord CreateSourceRecord(string code, bool isActive = true) => new()
    {
        Code = code,
        Name = $"Name {code}",
        ProviderCode = "google",
        ScopeCode = "BR",
        ExternalTaxonomyId = "ext-1",
        ExternalVersion = "2024-01",
        DefaultLanguage = "pt-BR",
        SourceUri = "https://example.com/taxonomy.txt",
        SourceChecksum = "abc123",
        IsActive = isActive,
        ImportedAt = DateTime.UtcNow
    };

    private static SourceTaxonomyNodeCreateRecord CreateNodeRecord(
        long sourceTaxonomyId,
        string externalNodeId,
        long? parentId = null,
        int level = 1,
        string nodeType = "category",
        bool isLeaf = false) => new()
    {
        SourceTaxonomyId = sourceTaxonomyId,
        ExternalNodeId = externalNodeId,
        ParentSourceTaxonomyNodeId = parentId,
        NodeType = nodeType,
        Name = $"Node {externalNodeId}",
        FullPath = $"Root > Node {externalNodeId}",
        Level = level,
        IsLeaf = isLeaf,
        IsActive = true,
        SourceLanguage = "pt-BR",
        ImportedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Migration_014_Can_Execute_Against_Test_Database()
    {
        var descriptor = await _repository.GetActiveAsync(CancellationToken.None);

        Assert.NotNull(descriptor);
    }

    [Fact]
    public async Task Migration_014_Rerun_Succeeds_Without_Duplicating_Schema_Objects()
    {
        await RunScriptAsync(_connectionString, "014-create-source-taxonomy-foundation.sql");

        var sourceId = await _repository.CreateAsync(CreateSourceRecord("rerun-check"), CancellationToken.None);
        Assert.True(sourceId > 0);
    }

    [Fact]
    public async Task CreateAsync_Should_Insert_And_Be_Readable_By_Id()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("code-by-id"), CancellationToken.None);

        var persisted = await _repository.GetByIdAsync(sourceId, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("code-by-id", persisted!.Code);
    }

    [Fact]
    public async Task GetByCodeAsync_Should_Return_Persisted_Taxonomy()
    {
        await _repository.CreateAsync(CreateSourceRecord("code-lookup"), CancellationToken.None);

        var persisted = await _repository.GetByCodeAsync("code-lookup", CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("code-lookup", persisted!.Code);
    }

    [Fact]
    public async Task Descriptor_Metadata_Should_Round_Trip()
    {
        var record = CreateSourceRecord("full-metadata");

        var sourceId = await _repository.CreateAsync(record, CancellationToken.None);
        var persisted = await _repository.GetByIdAsync(sourceId, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal(record.ProviderCode, persisted!.ProviderCode);
        Assert.Equal(record.ScopeCode, persisted.ScopeCode);
        Assert.Equal(record.ExternalTaxonomyId, persisted.ExternalTaxonomyId);
        Assert.Equal(record.ExternalVersion, persisted.ExternalVersion);
        Assert.Equal(record.DefaultLanguage, persisted.DefaultLanguage);
        Assert.Equal(record.SourceUri, persisted.SourceUri);
        Assert.Equal(record.SourceChecksum, persisted.SourceChecksum);
        Assert.Equal(record.IsActive, persisted.IsActive);
        Assert.True((DateTime.UtcNow - persisted.ImportedAt) < TimeSpan.FromMinutes(5));
        Assert.Null(persisted.UpdatedAt);
    }

    [Fact]
    public async Task GetActiveAsync_Should_Exclude_Inactive_Taxonomies()
    {
        await _repository.CreateAsync(CreateSourceRecord("active-one", isActive: true), CancellationToken.None);
        await _repository.CreateAsync(CreateSourceRecord("inactive-one", isActive: false), CancellationToken.None);

        var active = await _repository.GetActiveAsync(CancellationToken.None);

        Assert.Contains(active, item => item.Code == "active-one");
        Assert.DoesNotContain(active, item => item.Code == "inactive-one");
    }

    [Fact]
    public async Task CreateNodeAsync_Should_Allow_Multiple_Root_Nodes()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("multi-root"), CancellationToken.None);

        var root1 = await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "root-1"), CancellationToken.None);
        var root2 = await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "root-2"), CancellationToken.None);

        var roots = await _repository.GetRootsAsync(sourceId, CancellationToken.None);

        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, node => node.SourceTaxonomyNodeId == root1);
        Assert.Contains(roots, node => node.SourceTaxonomyNodeId == root2);
    }

    [Fact]
    public async Task CreateNodeAsync_Parent_Child_Should_Be_Readable_As_Children()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("parent-child"), CancellationToken.None);

        var parentId = await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "parent"), CancellationToken.None);
        var childId = await _repository.CreateNodeAsync(
            CreateNodeRecord(sourceId, "child", parentId: parentId, level: 2, isLeaf: true),
            CancellationToken.None);

        var children = await _repository.GetChildrenAsync(sourceId, parentId, CancellationToken.None);

        Assert.Single(children);
        Assert.Equal(childId, children.First().SourceTaxonomyNodeId);
    }

    [Fact]
    public async Task GetNodeByIdAsync_Should_Require_Both_SourceTaxonomyId_And_NodeId()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("node-by-id"), CancellationToken.None);
        var nodeId = await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "node-a"), CancellationToken.None);

        var found = await _repository.GetNodeByIdAsync(sourceId, nodeId, CancellationToken.None);
        var notFoundWrongSource = await _repository.GetNodeByIdAsync(sourceId + 999_999, nodeId, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Null(notFoundWrongSource);
    }

    [Fact]
    public async Task GetNodeByExternalIdAsync_Should_Require_Both_SourceTaxonomyId_And_ExternalNodeId()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("node-by-ext"), CancellationToken.None);
        await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "MLB1055"), CancellationToken.None);

        var found = await _repository.GetNodeByExternalIdAsync(sourceId, "MLB1055", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("MLB1055", found!.ExternalNodeId);
    }

    [Fact]
    public async Task Same_ExternalNodeId_Should_Be_Allowed_Across_Different_SourceTaxonomies()
    {
        var sourceA = await _repository.CreateAsync(CreateSourceRecord("source-a"), CancellationToken.None);
        var sourceB = await _repository.CreateAsync(CreateSourceRecord("source-b"), CancellationToken.None);

        await _repository.CreateNodeAsync(CreateNodeRecord(sourceA, "shared-ext"), CancellationToken.None);
        await _repository.CreateNodeAsync(CreateNodeRecord(sourceB, "shared-ext"), CancellationToken.None);

        var foundInA = await _repository.GetNodeByExternalIdAsync(sourceA, "shared-ext", CancellationToken.None);
        var foundInB = await _repository.GetNodeByExternalIdAsync(sourceB, "shared-ext", CancellationToken.None);

        Assert.NotNull(foundInA);
        Assert.NotNull(foundInB);
        Assert.Equal(sourceA, foundInA!.SourceTaxonomyId);
        Assert.Equal(sourceB, foundInB!.SourceTaxonomyId);
    }

    [Fact]
    public async Task Duplicate_ExternalNodeId_Inside_Same_SourceTaxonomy_Should_Be_Rejected()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("duplicate-ext"), CancellationToken.None);
        await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "dup-1"), CancellationToken.None);

        await Assert.ThrowsAsync<SqlException>(async () =>
            await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "dup-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Cross_Taxonomy_Parent_Assignment_Should_Be_Rejected()
    {
        var sourceA = await _repository.CreateAsync(CreateSourceRecord("cross-a"), CancellationToken.None);
        var sourceB = await _repository.CreateAsync(CreateSourceRecord("cross-b"), CancellationToken.None);

        var parentInA = await _repository.CreateNodeAsync(CreateNodeRecord(sourceA, "parent-in-a"), CancellationToken.None);

        await Assert.ThrowsAsync<SqlException>(async () =>
            await _repository.CreateNodeAsync(
                CreateNodeRecord(sourceB, "child-in-b", parentId: parentInA, level: 2),
                CancellationToken.None));
    }

    [Fact]
    public async Task Root_Count_Is_Not_Restricted_To_One()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("root-count"), CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, $"root-{i}"), CancellationToken.None);
        }

        var roots = await _repository.GetRootsAsync(sourceId, CancellationToken.None);

        Assert.Equal(3, roots.Count);
    }

    [Fact]
    public async Task Lookup_Scoped_To_SourceTaxonomy_A_Never_Returns_Node_Belonging_To_SourceTaxonomy_B()
    {
        var sourceA = await _repository.CreateAsync(CreateSourceRecord("scope-a"), CancellationToken.None);
        var sourceB = await _repository.CreateAsync(CreateSourceRecord("scope-b"), CancellationToken.None);

        await _repository.CreateNodeAsync(CreateNodeRecord(sourceB, "only-in-b"), CancellationToken.None);

        var foundInA = await _repository.GetNodeByExternalIdAsync(sourceA, "only-in-b", CancellationToken.None);

        Assert.Null(foundInA);
    }

    [Fact]
    public async Task NodeType_Can_Contain_Arbitrary_Provider_Neutral_Value()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("arbitrary-nodetype"), CancellationToken.None);

        var nodeId = await _repository.CreateNodeAsync(
            CreateNodeRecord(sourceId, "arbitrary-node", nodeType: "client-erp-segment"),
            CancellationToken.None);

        var persisted = await _repository.GetNodeByIdAsync(sourceId, nodeId, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("client-erp-segment", persisted!.NodeType);
    }

    [Fact]
    public async Task ExternalNodeId_Accepts_NonNumeric_Identifier()
    {
        var sourceId = await _repository.CreateAsync(CreateSourceRecord("non-numeric-ext"), CancellationToken.None);

        var nodeId = await _repository.CreateNodeAsync(CreateNodeRecord(sourceId, "MLB1055"), CancellationToken.None);

        var persisted = await _repository.GetNodeByIdAsync(sourceId, nodeId, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("MLB1055", persisted!.ExternalNodeId);
    }

    [Fact]
    public async Task Phase1_Level_And_Name_Indexes_Should_Exist()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            SELECT name FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Catalog.SourceTaxonomyNodes')
              AND name IN
              (
                  N'IX_SourceTaxonomyNodes_SourceTaxonomyId_Level',
                  N'IX_SourceTaxonomyNodes_SourceTaxonomyId_Name'
              )
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var found = new List<string>();
        while (await reader.ReadAsync())
        {
            found.Add(reader.GetString(0));
        }

        Assert.Contains("IX_SourceTaxonomyNodes_SourceTaxonomyId_Level", found);
        Assert.Contains("IX_SourceTaxonomyNodes_SourceTaxonomyId_Name", found);
    }
}
