using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Google;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Synchronization.InMemory;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Structural parity integration tests proving the complete Phase 4 flow
/// (docs task: "GoogleSourceTaxonomyAdapter + Structural Parity"):
///
/// Catalog.GoogleTaxonomyCategories
///     -> GoogleSourceTaxonomyAdapter
///     -> SourceTaxonomySnapshot
///     -> SourceTaxonomyImportOrchestrator
///     -> Catalog.SourceTaxonomyNodes
///
/// Both migration 001 (Google native tables) and migration 014 (generic
/// SourceTaxonomy foundation) are executed against the same real SQL Server
/// container, matching the existing repository-level integration test
/// conventions in this project.
/// </summary>
public sealed class GoogleSourceTaxonomyAdapterIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private string _connectionString = null!;
    private SqlSourceTaxonomyRepository _sourceRepository = null!;
    private SqlSourceTaxonomyImportStore _importStore = null!;
    private SqlSourceTaxonomySynchronizationStore _synchronizationStore = null!;
    private GoogleSourceTaxonomyAdapter _adapter = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        _connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync("001-google-taxonomy-tables.sql");
        await RunScriptAsync("014-create-source-taxonomy-foundation.sql");

        _sourceRepository = new SqlSourceTaxonomyRepository(_connectionString);
        _importStore = new SqlSourceTaxonomyImportStore(_connectionString);
        _synchronizationStore = new SqlSourceTaxonomySynchronizationStore(_connectionString);

        var options = Options.Create(new GoogleTaxonomySqlOptions { ConnectionString = _connectionString });
        _adapter = new GoogleSourceTaxonomyAdapter(options);
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    private async Task RunScriptAsync(string fileName)
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "deploy", "databases", "sqlserver", fileName);

        var script = await File.ReadAllTextAsync(Path.GetFullPath(scriptPath));

        await using var connection = new SqlConnection(_connectionString);
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

    private SourceTaxonomyImportOrchestrator CreateOrchestrator()
    {
        return new SourceTaxonomyImportOrchestrator(
            _sourceRepository,
            new[] { _adapter },
            _importStore,
            _synchronizationStore,
            new InMemorySourceTaxonomyImportGuard(),
            NullLogger<SourceTaxonomyImportOrchestrator>.Instance);
    }

    private async Task<long> CreateSourceTaxonomyAsync(string code, string providerCode = "google", string defaultLanguage = "en-US")
    {
        return await _sourceRepository.CreateAsync(new SourceTaxonomyCreateRecord
        {
            Code = code,
            Name = $"Google Product Taxonomy ({code})",
            ProviderCode = providerCode,
            ScopeCode = null,
            ExternalTaxonomyId = null,
            ExternalVersion = null,
            DefaultLanguage = defaultLanguage,
            SourceUri = "https://example.com/google-taxonomy.txt",
            SourceChecksum = null,
            IsActive = true,
            ImportedAt = DateTime.UtcNow
        }, CancellationToken.None);
    }

    private async Task InsertGoogleCategoryAsync(
        int googleCategoryId,
        int? parentGoogleCategoryId,
        string name,
        string fullPath,
        int level,
        bool isLeaf,
        bool isActive,
        string sourceLanguage = "en-US")
    {
        const string sql = """
            INSERT INTO [Catalog].[GoogleTaxonomyCategories]
                (GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage, CreatedAt, ImportedAt)
            VALUES
                (@GoogleCategoryId, @ParentGoogleCategoryId, @Name, @FullPath, @Level, @IsLeaf, @IsActive, @SourceLanguage, @Now, @Now)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@GoogleCategoryId", googleCategoryId);
        command.Parameters.AddWithValue("@ParentGoogleCategoryId", (object?)parentGoogleCategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@FullPath", fullPath);
        command.Parameters.AddWithValue("@Level", level);
        command.Parameters.AddWithValue("@IsLeaf", isLeaf);
        command.Parameters.AddWithValue("@IsActive", isActive);
        command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedRepresentativeGoogleTaxonomyAsync()
    {
        // Root 1, with a leaf and a non-leaf child.
        await InsertGoogleCategoryAsync(1, null, "Apparel & Accessories", "Apparel & Accessories", 0, isLeaf: false, isActive: true);
        await InsertGoogleCategoryAsync(2, 1, "Clothing", "Apparel & Accessories > Clothing", 1, isLeaf: false, isActive: true);
        await InsertGoogleCategoryAsync(3, 2, "Shirts", "Apparel & Accessories > Clothing > Shirts", 2, isLeaf: true, isActive: true);

        // Inactive node under Root 1.
        await InsertGoogleCategoryAsync(4, 2, "Discontinued Pants", "Apparel & Accessories > Clothing > Discontinued Pants", 2, isLeaf: true, isActive: false);

        // Root 2, a separate top-level tree.
        await InsertGoogleCategoryAsync(5, null, "Electronics", "Electronics", 0, isLeaf: false, isActive: true);
        await InsertGoogleCategoryAsync(6, 5, "Cameras", "Electronics > Cameras", 1, isLeaf: true, isActive: true);
    }

    private async Task<int> GetGoogleNativeRowCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM [Catalog].[GoogleTaxonomyCategories]";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task AdapterCode_Is_Exactly_GoogleProductTaxonomy()
    {
        Assert.Equal("google-product-taxonomy", _adapter.AdapterCode);
    }

    [Fact]
    public async Task Import_Should_Achieve_Structural_Parity_With_Google_Native_Data()
    {
        await SeedRepresentativeGoogleTaxonomyAsync();

        var sourceId = await CreateSourceTaxonomyAsync("google-product-taxonomy-en-us");

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);

        Assert.Equal(6, result.NodeCount);
        Assert.Equal(6, result.InsertedCount);

        // 1. Row count parity.
        var nativeCount = await GetGoogleNativeRowCountAsync();
        Assert.Equal(6, nativeCount);
        Assert.Equal(nativeCount, result.NodeCount);

        // 2/3/4/5/6/7. Field-level + parent-relationship parity for every node.
        var root1 = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "1", CancellationToken.None);
        var clothing = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);
        var shirts = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "3", CancellationToken.None);
        var discontinuedPants = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "4", CancellationToken.None);
        var root2 = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "5", CancellationToken.None);
        var cameras = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "6", CancellationToken.None);

        Assert.NotNull(root1);
        Assert.NotNull(clothing);
        Assert.NotNull(shirts);
        Assert.NotNull(discontinuedPants);
        Assert.NotNull(root2);
        Assert.NotNull(cameras);

        Assert.Null(root1!.ParentSourceTaxonomyNodeId);
        Assert.Equal(root1.SourceTaxonomyNodeId, clothing!.ParentSourceTaxonomyNodeId);
        Assert.Equal(clothing.SourceTaxonomyNodeId, shirts!.ParentSourceTaxonomyNodeId);
        Assert.Equal(clothing.SourceTaxonomyNodeId, discontinuedPants!.ParentSourceTaxonomyNodeId);
        Assert.Null(root2!.ParentSourceTaxonomyNodeId);
        Assert.Equal(root2.SourceTaxonomyNodeId, cameras!.ParentSourceTaxonomyNodeId);

        Assert.Equal("Apparel & Accessories", root1.Name);
        Assert.Equal("Apparel & Accessories", root1.FullPath);
        Assert.Equal(0, root1.Level);
        Assert.False(root1.IsLeaf);
        Assert.True(root1.IsActive);

        Assert.Equal("Shirts", shirts.Name);
        Assert.Equal("Apparel & Accessories > Clothing > Shirts", shirts.FullPath);
        Assert.Equal(2, shirts.Level);
        Assert.True(shirts.IsLeaf);
        Assert.True(shirts.IsActive);

        // 8. Inactive node preservation.
        Assert.Equal("Discontinued Pants", discontinuedPants.Name);
        Assert.False(discontinuedPants.IsActive);

        // 9. SourceLanguage / snapshot locale parity.
        Assert.Equal("en-US", root1.SourceLanguage);
        Assert.Equal("en-US", discontinuedPants.SourceLanguage);

        // Multiple roots supported, no single-root rule.
        var roots = await _sourceRepository.GetRootsAsync(sourceId, CancellationToken.None);
        Assert.Equal(2, roots.Count);
    }

    [Fact]
    public async Task Repeat_Import_Should_Not_Duplicate_Nodes_Or_Corrupt_Hierarchy()
    {
        await SeedRepresentativeGoogleTaxonomyAsync();

        var sourceId = await CreateSourceTaxonomyAsync("repeat-import");

        var firstResult = await CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);
        var secondResult = await CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);

        Assert.Equal(6, firstResult.NodeCount);
        Assert.Equal(6, secondResult.NodeCount);

        var roots = await _sourceRepository.GetRootsAsync(sourceId, CancellationToken.None);
        Assert.Equal(2, roots.Count);

        var clothing = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "2", CancellationToken.None);
        var shirts = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, "3", CancellationToken.None);
        Assert.Equal(clothing!.SourceTaxonomyNodeId, shirts!.ParentSourceTaxonomyNodeId);
    }

    [Fact]
    public async Task Import_Should_Not_Mutate_Google_Native_Data()
    {
        await SeedRepresentativeGoogleTaxonomyAsync();

        var beforeCount = await GetGoogleNativeRowCountAsync();

        var sourceId = await CreateSourceTaxonomyAsync("isolation-check");
        await CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);

        var afterCount = await GetGoogleNativeRowCountAsync();

        Assert.Equal(beforeCount, afterCount);

        var discontinued = await new SqlGoogleTaxonomyRepository(Options.Create(new GoogleTaxonomySqlOptions { ConnectionString = _connectionString }))
            .GetByIdAsync(4, CancellationToken.None);

        Assert.NotNull(discontinued);
        Assert.False(discontinued!.IsActive);
    }

    [Fact]
    public async Task Import_With_Mismatched_ProviderCode_Should_Be_Rejected()
    {
        await SeedRepresentativeGoogleTaxonomyAsync();

        var sourceId = await CreateSourceTaxonomyAsync("wrong-provider", providerCode: "mercadolivre");

        await Assert.ThrowsAsync<SourceTaxonomyProviderMismatchException>(
            () => CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None));
    }

    [Fact]
    public async Task Import_With_Incompatible_DefaultLanguage_Should_Fail()
    {
        await SeedRepresentativeGoogleTaxonomyAsync();

        var sourceId = await CreateSourceTaxonomyAsync("wrong-language", defaultLanguage: "pt-BR");

        await Assert.ThrowsAsync<GoogleSourceTaxonomyLanguageMismatchException>(
            () => CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None));
    }
}
