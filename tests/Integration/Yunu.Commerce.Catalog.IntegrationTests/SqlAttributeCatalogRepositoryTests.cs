using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Infrastructure.AttributeCatalog.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for SqlAttributeCatalogRepository against a real SQL
/// Server instance via Testcontainers (docs task: "SKU attribute foundation").
/// The schema is created by executing
/// deploy/databases/sqlserver/002_create_sku_attribute_catalog.sql directly against the
/// container, which also seeds the reference AttributeGroups/
/// AttributeDefinitions/AttributeOptions rows used by these assertions.
/// </summary>
public sealed class SqlAttributeCatalogRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private SqlAttributeCatalogRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var connectionString = _sqlContainer.GetConnectionString();

        await CreateSchemaAsync(connectionString);

        var options = Options.Create(new GoogleTaxonomySqlOptions
        {
            ConnectionString = connectionString
        });

        _repository = new SqlAttributeCatalogRepository(options);
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    private static async Task CreateSchemaAsync(string connectionString)
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "deploy", "databases", "sqlserver", "002_create_sku_attribute_catalog.sql");

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
        var definition = await _repository.GetDefinitionByCodeAsync("color", CancellationToken.None);

        Assert.NotNull(definition);
        Assert.Equal(14, definition!.AttributeDefinitionId);
        Assert.Equal("Text", definition.DataType);
        Assert.True(definition.IsActive);
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
        var definition = await _repository.GetDefinitionByIdAsync(47, CancellationToken.None);

        Assert.NotNull(definition);
        Assert.Equal("gender", definition!.Code);
        Assert.Equal("Enum", definition.DataType);
    }

    [Fact]
    public async Task GetOptionAsync_For_Existing_Definition_And_Code_Should_Return_Option()
    {
        var option = await _repository.GetOptionAsync(47, "MALE", CancellationToken.None);

        Assert.NotNull(option);
        Assert.Equal(1401, option!.AttributeOptionId);
        Assert.True(option.IsActive);
    }

    [Fact]
    public async Task GetOptionAsync_For_Unknown_Code_Should_Return_Null()
    {
        var option = await _repository.GetOptionAsync(47, "NON_EXISTENT", CancellationToken.None);

        Assert.Null(option);
    }
}
