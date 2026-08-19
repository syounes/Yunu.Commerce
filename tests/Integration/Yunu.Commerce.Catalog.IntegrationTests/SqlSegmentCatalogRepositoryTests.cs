using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for SqlSegmentCatalogRepository against a real SQL
/// Server instance via Testcontainers (docs task: "Canonical Taxonomy +
/// Segments Domain" §23-§24). The schema is created by executing
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql
/// and deploy/databases/sqlserver/008-add-segment-assignment-scope.sql
/// directly against the container, which also seeds the initial
/// SegmentDefinitions ("target_audience", "gender", "sport_modality", etc.).
/// </summary>
public sealed class SqlSegmentCatalogRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private SqlSegmentCatalogRepository _repository = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        _connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync(_connectionString, "006-create-canonical-taxonomy-segmentation.sql");
        await RunScriptAsync(_connectionString, "008-add-segment-assignment-scope.sql");

        var options = Options.Create(new GoogleTaxonomySqlOptions
        {
            ConnectionString = _connectionString
        });

        _repository = new SqlSegmentCatalogRepository(options);
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

    [Fact]
    public async Task GetDefinitionByCodeAsync_For_Existing_Code_Should_Return_Definition()
    {
        var definition = await _repository.GetDefinitionByCodeAsync("gender", CancellationToken.None);

        Assert.NotNull(definition);
        Assert.Equal("gender", definition!.Code);
        Assert.Equal("Single", definition.SelectionMode);
        Assert.Equal("ProductWithSkuOverride", definition.AssignmentScope);
        Assert.Equal("Active", definition.Status);
    }

    [Fact]
    public async Task GetDefinitionByCodeAsync_For_Unknown_Code_Should_Return_Null()
    {
        var definition = await _repository.GetDefinitionByCodeAsync("does_not_exist", CancellationToken.None);

        Assert.Null(definition);
    }

    [Fact]
    public async Task GetDefinitionByIdAsync_For_Existing_Id_Should_Return_Definition()
    {
        var byCode = await _repository.GetDefinitionByCodeAsync("sport_modality", CancellationToken.None);

        var definition = await _repository.GetDefinitionByIdAsync(byCode!.SegmentDefinitionId, CancellationToken.None);

        Assert.NotNull(definition);
        Assert.Equal("sport_modality", definition!.Code);
        Assert.Equal("Product", definition.AssignmentScope);
    }

    [Fact]
    public async Task GetDefinitionByIdAsync_For_Unknown_Id_Should_Return_Null()
    {
        var definition = await _repository.GetDefinitionByIdAsync(999_999, CancellationToken.None);

        Assert.Null(definition);
    }

    [Fact]
    public async Task GetDefinitionsAsync_Should_Return_All_Seeded_Definitions()
    {
        var definitions = await _repository.GetDefinitionsAsync(CancellationToken.None);

        Assert.NotEmpty(definitions);
        Assert.Contains(definitions, d => d.Code == "gender");
        Assert.Contains(definitions, d => d.Code == "target_audience");
    }

    [Fact]
    public async Task GetOptionAsync_Should_Not_Return_Option_Belonging_To_Another_Definition()
    {
        var genderDefinition = await _repository.GetDefinitionByCodeAsync("gender", CancellationToken.None);
        var targetAudienceDefinition = await _repository.GetDefinitionByCodeAsync("target_audience", CancellationToken.None);

        Assert.NotNull(genderDefinition);
        Assert.NotNull(targetAudienceDefinition);

        var targetAudienceOptions = await _repository.GetOptionsByDefinitionAsync(targetAudienceDefinition!.SegmentDefinitionId, CancellationToken.None);
        Assert.NotEmpty(targetAudienceOptions);

        var optionCodeFromOtherDefinition = targetAudienceOptions.First().Code;

        var result = await _repository.GetOptionAsync(genderDefinition!.SegmentDefinitionId, optionCodeFromOtherDefinition, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOptionsByDefinitionAsync_Should_Return_Only_Options_For_That_Definition_Ordered_By_DisplayOrder()
    {
        var definition = await _repository.GetDefinitionByCodeAsync("gender", CancellationToken.None);

        var options = await _repository.GetOptionsByDefinitionAsync(definition!.SegmentDefinitionId, CancellationToken.None);

        Assert.NotEmpty(options);
        Assert.All(options, option => Assert.Equal(definition.SegmentDefinitionId, option.SegmentDefinitionId));

        var displayOrders = options.Select(o => o.DisplayOrder).ToArray();
        Assert.Equal(displayOrders.OrderBy(o => o), displayOrders);
    }
}
