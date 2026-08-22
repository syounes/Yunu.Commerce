using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Synchronization.InMemory;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Real SQL Server integration tests for the Phase 3 generic SourceTaxonomy
/// import orchestration (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §9-§18). Uses Testcontainers and executes migration 014 directly; a fake
/// <see cref="ISourceTaxonomyAdapter"/> supplies normalized snapshots so no
/// concrete provider adapter is required.
/// </summary>
public sealed class SourceTaxonomyImportOrchestratorIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private string _connectionString = null!;
    private SqlSourceTaxonomyRepository _sourceRepository = null!;
    private SqlSourceTaxonomyImportStore _importStore = null!;
    private SqlSourceTaxonomySynchronizationStore _synchronizationStore = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        _connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync(_connectionString, "014-create-source-taxonomy-foundation.sql");

        _sourceRepository = new SqlSourceTaxonomyRepository(_connectionString);
        _importStore = new SqlSourceTaxonomyImportStore(_connectionString);
        _synchronizationStore = new SqlSourceTaxonomySynchronizationStore(_connectionString);
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

    private SourceTaxonomyImportOrchestrator CreateOrchestrator(params ISourceTaxonomyAdapter[] adapters)
    {
        return new SourceTaxonomyImportOrchestrator(
            _sourceRepository,
            adapters,
            _importStore,
            _synchronizationStore,
            new InMemorySourceTaxonomyImportGuard(),
            NullLogger<SourceTaxonomyImportOrchestrator>.Instance);
    }

    private async Task<long> CreateSourceTaxonomyAsync(string code, string providerCode = "fake-provider", string? scopeCode = null, string? externalTaxonomyId = null)
    {
        return await _sourceRepository.CreateAsync(new SourceTaxonomyCreateRecord
        {
            Code = code,
            Name = $"Name {code}",
            ProviderCode = providerCode,
            ScopeCode = scopeCode,
            ExternalTaxonomyId = externalTaxonomyId,
            ExternalVersion = null,
            DefaultLanguage = "pt-BR",
            SourceUri = null,
            SourceChecksum = null,
            IsActive = true,
            ImportedAt = DateTime.UtcNow
        }, CancellationToken.None);
    }

    private static ISourceTaxonomyAdapter FakeAdapter(
        string adapterCode,
        Func<SourceTaxonomyImportContext, SourceTaxonomySnapshot> snapshotFactory)
        => new DelegateSourceTaxonomyAdapter(adapterCode, snapshotFactory);

    private static SourceTaxonomySnapshotNode Node(
        string externalNodeId,
        string? parentExternalNodeId = null,
        string name = "Node",
        string fullPath = "Root",
        int level = 0,
        bool isLeaf = true,
        bool isActive = true,
        string nodeType = "Category") => new()
    {
        ExternalNodeId = externalNodeId,
        ParentExternalNodeId = parentExternalNodeId,
        NodeType = nodeType,
        Name = name,
        FullPath = fullPath,
        Level = level,
        IsLeaf = isLeaf,
        IsActive = isActive
    };

    private static SourceTaxonomySnapshotDescriptor Descriptor(string? checksum = null, string providerCode = "fake-provider") => new()
    {
        ProviderCode = providerCode,
        Locale = "pt-BR",
        ExternalVersion = "v1",
        SourceUri = "https://example.com/taxonomy",
        SourceChecksum = checksum
    };

    [Fact]
    public async Task FirstSnapshot_Should_Insert_All_Nodes()
    {
        var sourceId = await CreateSourceTaxonomyAsync("first-import");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "checksum-1"),
            Nodes = new[]
            {
                Node("1", name: "Root", fullPath: "Root", level: 0, isLeaf: false),
                Node("2", parentExternalNodeId: "1", name: "Child", fullPath: "Root > Child", level: 1, isLeaf: true)
            }
        };

        var adapter = FakeAdapter("fake", _ => snapshot);
        var orchestrator = CreateOrchestrator(adapter);

        var result = await orchestrator.ImportAsync(sourceId, "fake", CancellationToken.None);

        Assert.Equal(2, result.NodeCount);
        Assert.Equal(2, result.InsertedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.DeactivatedCount);

        var root = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "1", CancellationToken.None);
        var child = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);

        Assert.NotNull(root);
        Assert.NotNull(child);
        Assert.Null(root!.ParentSourceTaxonomyNodeId);
        Assert.Equal(root.SourceTaxonomyNodeId, child!.ParentSourceTaxonomyNodeId);
    }

    [Fact]
    public async Task SecondIdenticalSnapshot_With_SameChecksum_Should_Skip_Node_Rewrites()
    {
        var sourceId = await CreateSourceTaxonomyAsync("checksum-skip");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "same-checksum"),
            Nodes = new[] { Node("1", name: "Root", fullPath: "Root") }
        };

        var adapter = FakeAdapter("fake", _ => snapshot);
        var orchestrator = CreateOrchestrator(adapter);

        await orchestrator.ImportAsync(sourceId, "fake", CancellationToken.None);

        var secondResult = await orchestrator.ImportAsync(sourceId, "fake", CancellationToken.None);

        Assert.Equal(0, secondResult.InsertedCount);
        Assert.Equal(0, secondResult.UpdatedCount);
        Assert.Equal(0, secondResult.DeactivatedCount);
    }

    [Fact]
    public async Task ChangedChecksum_With_ChangedName_Should_Update_One_Node()
    {
        var sourceId = await CreateSourceTaxonomyAsync("changed-name");

        var firstSnapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "checksum-a"),
            Nodes = new[] { Node("1", name: "Old Name", fullPath: "Root") }
        };

        await CreateOrchestrator(FakeAdapter("fake", _ => firstSnapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var secondSnapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "checksum-b"),
            Nodes = new[] { Node("1", name: "New Name", fullPath: "Root") }
        };

        var result = await CreateOrchestrator(FakeAdapter("fake", _ => secondSnapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        Assert.Equal(0, result.InsertedCount);
        Assert.Equal(1, result.UpdatedCount);

        var node = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "1", CancellationToken.None);
        Assert.Equal("New Name", node!.Name);
    }

    [Fact]
    public async Task ChangedFullPath_Should_Update_Node()
    {
        var sourceId = await CreateSourceTaxonomyAsync("changed-path");

        var first = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("1", fullPath: "Old Path") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => first)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var second = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c2"),
            Nodes = new[] { Node("1", fullPath: "New Path") }
        };
        var result = await CreateOrchestrator(FakeAdapter("fake", _ => second)).ImportAsync(sourceId, "fake", CancellationToken.None);

        Assert.Equal(1, result.UpdatedCount);
        var node = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "1", CancellationToken.None);
        Assert.Equal("New Path", node!.FullPath);
    }

    [Fact]
    public async Task ChangedHierarchy_Should_Move_Node_To_Another_Parent()
    {
        var sourceId = await CreateSourceTaxonomyAsync("moved-node");

        var first = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[]
            {
                Node("parent-a", name: "Parent A", fullPath: "A"),
                Node("parent-b", name: "Parent B", fullPath: "B"),
                Node("child", parentExternalNodeId: "parent-a", name: "Child", fullPath: "A > Child", level: 1)
            }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => first)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var second = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c2"),
            Nodes = new[]
            {
                Node("parent-a", name: "Parent A", fullPath: "A"),
                Node("parent-b", name: "Parent B", fullPath: "B"),
                Node("child", parentExternalNodeId: "parent-b", name: "Child", fullPath: "B > Child", level: 1)
            }
        };
        var result = await CreateOrchestrator(FakeAdapter("fake", _ => second)).ImportAsync(sourceId, "fake", CancellationToken.None);

        Assert.True(result.UpdatedCount >= 1);

        var parentB = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "parent-b", CancellationToken.None);
        var child = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "child", CancellationToken.None);

        Assert.Equal(parentB!.SourceTaxonomyNodeId, child!.ParentSourceTaxonomyNodeId);
    }

    [Fact]
    public async Task ReappearingNode_Should_Reactivate()
    {
        var sourceId = await CreateSourceTaxonomyAsync("reactivate");

        var first = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("1"), Node("2") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => first)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var second = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c2"),
            Nodes = new[] { Node("1") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => second)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var deactivated = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);
        Assert.False(deactivated!.IsActive);

        var third = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c3"),
            Nodes = new[] { Node("1"), Node("2") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => third)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var reactivated = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task NodeRemovedFromSnapshot_Should_Become_Inactive_Not_HardDeleted()
    {
        var sourceId = await CreateSourceTaxonomyAsync("removed-node");

        var first = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("1"), Node("2") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => first)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var second = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c2"),
            Nodes = new[] { Node("1") }
        };
        var result = await CreateOrchestrator(FakeAdapter("fake", _ => second)).ImportAsync(sourceId, "fake", CancellationToken.None);

        Assert.Equal(1, result.DeactivatedCount);

        var node = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);
        Assert.NotNull(node);
        Assert.False(node!.IsActive);
    }

    [Fact]
    public async Task MultipleRoots_Should_Be_Preserved()
    {
        var sourceId = await CreateSourceTaxonomyAsync("multi-root-import");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("root-1"), Node("root-2") }
        };

        await CreateOrchestrator(FakeAdapter("fake", _ => snapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var roots = await _sourceRepository.GetRootsAsync(sourceId, CancellationToken.None);
        Assert.Equal(2, roots.Count);
    }

    [Fact]
    public async Task SameExternalNodeId_In_Different_SourceTaxonomies_Should_Remain_Isolated()
    {
        var sourceA = await CreateSourceTaxonomyAsync("source-a");
        var sourceB = await CreateSourceTaxonomyAsync("source-b");

        var snapshotA = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "a1"),
            Nodes = new[] { Node("shared-id", name: "A Name") }
        };
        var snapshotB = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "b1"),
            Nodes = new[] { Node("shared-id", name: "B Name") }
        };

        await CreateOrchestrator(FakeAdapter("fake", _ => snapshotA)).ImportAsync(sourceA, "fake", CancellationToken.None);
        await CreateOrchestrator(FakeAdapter("fake", _ => snapshotB)).ImportAsync(sourceB, "fake", CancellationToken.None);

        var nodeA = await _sourceRepository.GetNodeByExternalIdAsync(sourceA, "shared-id", CancellationToken.None);
        var nodeB = await _sourceRepository.GetNodeByExternalIdAsync(sourceB, "shared-id", CancellationToken.None);

        Assert.Equal("A Name", nodeA!.Name);
        Assert.Equal("B Name", nodeB!.Name);
    }

    [Fact]
    public async Task ImportingSourceA_Should_Never_Affect_SourceB_Nodes()
    {
        var sourceA = await CreateSourceTaxonomyAsync("isolated-a");
        var sourceB = await CreateSourceTaxonomyAsync("isolated-b");

        var snapshotB = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "b1"),
            Nodes = new[] { Node("b-node") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => snapshotB)).ImportAsync(sourceB, "fake", CancellationToken.None);

        var snapshotA = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "a1"),
            Nodes = new[] { Node("a-node") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => snapshotA)).ImportAsync(sourceA, "fake", CancellationToken.None);

        var bNode = await _sourceRepository.GetNodeByExternalIdAsync(sourceB, "b-node", CancellationToken.None);
        Assert.NotNull(bNode);
        Assert.True(bNode!.IsActive);
    }

    [Fact]
    public async Task ArbitraryNodeType_And_OpaqueExternalNodeId_Should_Persist()
    {
        var sourceId = await CreateSourceTaxonomyAsync("opaque-ids");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("MLB1055", nodeType: "BrowseNode") }
        };

        await CreateOrchestrator(FakeAdapter("fake", _ => snapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var node = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "MLB1055", CancellationToken.None);
        Assert.NotNull(node);
        Assert.Equal("BrowseNode", node!.NodeType);
    }

    [Fact]
    public async Task SourceTaxonomy_Header_Should_Refresh_Mutable_Metadata_After_Success()
    {
        var sourceId = await CreateSourceTaxonomyAsync("header-refresh");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = new SourceTaxonomySnapshotDescriptor
            {
                ProviderCode = "fake-provider",
                Locale = "en-US",
                ExternalVersion = "2024-05",
                SourceUri = "https://example.com/v2",
                SourceChecksum = "new-checksum"
            },
            Nodes = new[] { Node("1") }
        };

        await CreateOrchestrator(FakeAdapter("fake", _ => snapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var descriptor = await _sourceRepository.GetByIdAsync(sourceId, CancellationToken.None);

        Assert.Equal("2024-05", descriptor!.ExternalVersion);
        Assert.Equal("en-US", descriptor.DefaultLanguage);
        Assert.Equal("https://example.com/v2", descriptor.SourceUri);
        Assert.Equal("new-checksum", descriptor.SourceChecksum);
    }

    [Fact]
    public async Task Code_Name_ProviderCode_Should_Not_Be_Rewritten_By_Synchronization()
    {
        var sourceId = await CreateSourceTaxonomyAsync("identity-stable", providerCode: "fake-provider");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("1") }
        };

        await CreateOrchestrator(FakeAdapter("fake", _ => snapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var descriptor = await _sourceRepository.GetByIdAsync(sourceId, CancellationToken.None);

        Assert.Equal("identity-stable", descriptor!.Code);
        Assert.Equal("fake-provider", descriptor.ProviderCode);
    }

    [Fact]
    public async Task ImportHistory_Should_Persist_Started_Then_Completed()
    {
        var sourceId = await CreateSourceTaxonomyAsync("import-history");

        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("1"), Node("2", parentExternalNodeId: "1", level: 1) }
        };

        var result = await CreateOrchestrator(FakeAdapter("fake", _ => snapshot)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var importRow = await GetImportRowAsync(result.ImportId);

        Assert.Equal("Completed", importRow.Status);
        Assert.Null(importRow.ErrorMessage);
        Assert.NotNull(importRow.CompletedAt);
        Assert.Equal(2, importRow.NodeCount);
        Assert.Equal(2, importRow.InsertedCount);
        Assert.Equal("fake", importRow.AdapterCode);
    }

    [Fact]
    public async Task AdapterFailure_Should_Record_Failed_Without_Node_Mutations()
    {
        var sourceId = await CreateSourceTaxonomyAsync("adapter-failure");

        var orchestrator = CreateOrchestrator(new ThrowingSourceTaxonomyAdapter("fake", new InvalidOperationException("adapter failure")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ImportAsync(sourceId, "fake", CancellationToken.None));

        var roots = await _sourceRepository.GetRootsAsync(sourceId, CancellationToken.None);
        Assert.Empty(roots);
    }

    [Fact]
    public async Task EmptySnapshot_Should_Be_Rejected_And_Not_MassDeactivate()
    {
        var sourceId = await CreateSourceTaxonomyAsync("empty-snapshot-guard");

        var first = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c1"),
            Nodes = new[] { Node("1"), Node("2") }
        };
        await CreateOrchestrator(FakeAdapter("fake", _ => first)).ImportAsync(sourceId, "fake", CancellationToken.None);

        var empty = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(checksum: "c2"),
            Nodes = Array.Empty<SourceTaxonomySnapshotNode>()
        };

        await Assert.ThrowsAsync<SourceTaxonomySnapshotValidationException>(
            () => CreateOrchestrator(FakeAdapter("fake", _ => empty)).ImportAsync(sourceId, "fake", CancellationToken.None));

        var node1 = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "1", CancellationToken.None);
        var node2 = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);

        Assert.True(node1!.IsActive);
        Assert.True(node2!.IsActive);
    }

    private async Task<(string Status, string? ErrorMessage, DateTime? CompletedAt, int NodeCount, int InsertedCount, string AdapterCode)> GetImportRowAsync(long importId)
    {
        const string sql = """
            SELECT Status, ErrorMessage, CompletedAt, NodeCount, InsertedCount, AdapterCode
            FROM Integration.SourceTaxonomyImports
            WHERE ImportId = @ImportId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ImportId", importId);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetString(5));
    }

    private sealed class DelegateSourceTaxonomyAdapter : ISourceTaxonomyAdapter
    {
        private readonly Func<SourceTaxonomyImportContext, SourceTaxonomySnapshot> _snapshotFactory;

        public DelegateSourceTaxonomyAdapter(string adapterCode, Func<SourceTaxonomyImportContext, SourceTaxonomySnapshot> snapshotFactory)
        {
            AdapterCode = adapterCode;
            _snapshotFactory = snapshotFactory;
        }

        public string AdapterCode { get; }

        public Task<SourceTaxonomySnapshot> LoadAsync(SourceTaxonomyImportContext context, CancellationToken cancellationToken)
            => Task.FromResult(_snapshotFactory(context));
    }

    private sealed class ThrowingSourceTaxonomyAdapter : ISourceTaxonomyAdapter
    {
        private readonly Exception _exception;

        public ThrowingSourceTaxonomyAdapter(string adapterCode, Exception exception)
        {
            AdapterCode = adapterCode;
            _exception = exception;
        }

        public string AdapterCode { get; }

        public Task<SourceTaxonomySnapshot> LoadAsync(SourceTaxonomyImportContext context, CancellationToken cancellationToken)
            => throw _exception;
    }
}
