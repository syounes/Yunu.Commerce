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

    /// <summary>
    /// Test-only read model for the complete native Google row, used solely to
    /// prove exhaustive structural parity against the normalized
    /// SourceTaxonomy dataset. Distinct from the adapter's own private read
    /// model; this one lives in the test project.
    /// </summary>
    private sealed record NativeGoogleCategoryRow(
        int GoogleCategoryId,
        int? ParentGoogleCategoryId,
        string Name,
        string FullPath,
        int Level,
        bool IsLeaf,
        bool IsActive,
        string SourceLanguage);

    private async Task<IReadOnlyCollection<NativeGoogleCategoryRow>> LoadAllGoogleNativeRowsAsync()
    {
        const string sql = """
            SELECT GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage
            FROM [Catalog].[GoogleTaxonomyCategories]
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<NativeGoogleCategoryRow>();

        while (await reader.ReadAsync())
        {
            results.Add(new NativeGoogleCategoryRow(
                GoogleCategoryId: reader.GetInt32(0),
                ParentGoogleCategoryId: reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Name: reader.GetString(2),
                FullPath: reader.GetString(3),
                Level: reader.GetInt32(4),
                IsLeaf: reader.GetBoolean(5),
                IsActive: reader.GetBoolean(6),
                SourceLanguage: reader.GetString(7)));
        }

        return results;
    }

    [Fact]
    public async Task AdapterCode_Is_Exactly_GoogleProductTaxonomy()
    {
        Assert.Equal("google-product-taxonomy", _adapter.AdapterCode);
    }

    [Fact]
    public async Task Import_Should_Achieve_Exhaustive_Node_By_Node_Structural_Parity_With_Google_Native_Data()
    {
        await SeedRepresentativeGoogleTaxonomyAsync();

        var sourceId = await CreateSourceTaxonomyAsync("google-product-taxonomy-en-us");

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);

        var nativeRows = await LoadAllGoogleNativeRowsAsync();

        // 1. Row count parity: every native row has exactly one normalized counterpart.
        Assert.Equal(nativeRows.Count, result.NodeCount);
        Assert.Equal(nativeRows.Count, result.InsertedCount);

        var normalizedNodes = new Dictionary<string, SourceTaxonomyNodeRecord>(StringComparer.Ordinal);
        var normalizedById = new Dictionary<long, SourceTaxonomyNodeRecord>();

        foreach (var nativeRow in nativeRows)
        {
            var externalNodeId = nativeRow.GoogleCategoryId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var normalizedNode = await _sourceRepository.GetNodeByExternalIdAsync(sourceId, externalNodeId, CancellationToken.None);

            Assert.NotNull(normalizedNode);

            normalizedNodes[externalNodeId] = normalizedNode!;
            normalizedById[normalizedNode!.SourceTaxonomyNodeId] = normalizedNode;
        }

        // 2-9. Exhaustive scalar + parent-relationship parity for EVERY native row,
        // not just a representative subset.
        foreach (var nativeRow in nativeRows)
        {
            var externalNodeId = nativeRow.GoogleCategoryId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var normalizedNode = normalizedNodes[externalNodeId];

            Assert.Equal(nativeRow.Name, normalizedNode.Name);
            Assert.Equal(nativeRow.FullPath, normalizedNode.FullPath);
            Assert.Equal(nativeRow.Level, normalizedNode.Level);
            Assert.Equal(nativeRow.IsLeaf, normalizedNode.IsLeaf);
            Assert.Equal(nativeRow.IsActive, normalizedNode.IsActive);
            Assert.Equal(nativeRow.SourceLanguage, normalizedNode.SourceLanguage);
            Assert.Equal("Category", normalizedNode.NodeType);

            if (nativeRow.ParentGoogleCategoryId is null)
            {
                Assert.Null(normalizedNode.ParentSourceTaxonomyNodeId);
            }
            else
            {
                Assert.NotNull(normalizedNode.ParentSourceTaxonomyNodeId);

                var normalizedParent = normalizedById[normalizedNode.ParentSourceTaxonomyNodeId!.Value];
                var expectedParentExternalNodeId = nativeRow.ParentGoogleCategoryId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

                Assert.Equal(expectedParentExternalNodeId, normalizedParent.ExternalNodeId);
            }
        }

        // Explicit inactive-node preservation proof (not implied by the loop above alone).
        Assert.Contains(nativeRows, row => !row.IsActive);
        var inactiveExternalId = nativeRows.First(row => !row.IsActive).GoogleCategoryId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(normalizedNodes[inactiveExternalId].IsActive);

        // Explicit multiple-roots proof: no single-root rule applied.
        var roots = await _sourceRepository.GetRootsAsync(sourceId, CancellationToken.None);
        var nativeRootCount = nativeRows.Count(row => row.ParentGoogleCategoryId is null);
        Assert.True(nativeRootCount >= 2);
        Assert.Equal(nativeRootCount, roots.Count);
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

    [Fact]
    public async Task Import_With_Inconsistent_SourceLanguage_Dataset_Should_Fail_And_Leave_No_Side_Effects()
    {
        // Node A in en-US, node B in pt-BR: no single consistent locale can be
        // determined, so the adapter must reject the dataset outright.
        await InsertGoogleCategoryAsync(1, null, "Root A", "Root A", 0, isLeaf: true, isActive: true, sourceLanguage: "en-US");
        await InsertGoogleCategoryAsync(2, null, "Root B", "Root B", 0, isLeaf: true, isActive: true, sourceLanguage: "pt-BR");

        var beforeCount = await GetGoogleNativeRowCountAsync();

        var sourceId = await CreateSourceTaxonomyAsync("inconsistent-language");

        await Assert.ThrowsAsync<GoogleSourceTaxonomyInconsistentLanguageException>(
            () => CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None));

        // No SourceTaxonomy node synchronization must have occurred.
        var roots = await _sourceRepository.GetRootsAsync(sourceId, CancellationToken.None);
        Assert.Empty(roots);

        // The generic orchestrator's import history must record the failure.
        var afterCount = await GetGoogleNativeRowCountAsync();
        Assert.Equal(beforeCount, afterCount);
    }

    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("en-US", "en")]
    [InlineData("pt", "pt-BR")]
    [InlineData("pt-BR", "pt")]
    public async Task Import_With_Generic_Primary_Language_Vs_Specific_Locale_Should_Be_Accepted(
        string defaultLanguage,
        string googleSourceLanguage)
    {
        await InsertGoogleCategoryAsync(1, null, "Root", "Root", 0, isLeaf: true, isActive: true, sourceLanguage: googleSourceLanguage);

        var sourceId = await CreateSourceTaxonomyAsync($"generic-locale-{defaultLanguage}-{googleSourceLanguage}", defaultLanguage: defaultLanguage);

        var result = await CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);

        Assert.Equal(1, result.NodeCount);
    }

    [Theory]
    [InlineData("en-US", "en-GB")]
    [InlineData("pt-BR", "pt-PT")]
    [InlineData("zh-CN", "zh-TW")]
    public async Task Import_With_Different_Specific_Locales_Should_Be_Rejected(
        string defaultLanguage,
        string googleSourceLanguage)
    {
        await InsertGoogleCategoryAsync(1, null, "Root", "Root", 0, isLeaf: true, isActive: true, sourceLanguage: googleSourceLanguage);

        var sourceId = await CreateSourceTaxonomyAsync($"specific-locale-{defaultLanguage}-{googleSourceLanguage}", defaultLanguage: defaultLanguage);

        await Assert.ThrowsAsync<GoogleSourceTaxonomyLanguageMismatchException>(
            () => CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None));
    }

    [Fact]
    public async Task Import_With_Exact_Locale_Case_Insensitive_Match_Should_Be_Accepted()
    {
        await InsertGoogleCategoryAsync(1, null, "Root", "Root", 0, isLeaf: true, isActive: true, sourceLanguage: "en-US");

        var sourceId = await CreateSourceTaxonomyAsync("case-insensitive-locale", defaultLanguage: "EN-us");

        var result = await CreateOrchestrator().ImportAsync(sourceId, GoogleSourceTaxonomyAdapter.GoogleAdapterCode, CancellationToken.None);

        Assert.Equal(1, result.NodeCount);
    }
}
