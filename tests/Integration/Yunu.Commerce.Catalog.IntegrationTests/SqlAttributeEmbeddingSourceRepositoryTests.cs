using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for SqlAttributeEmbeddingSourceRepository against a real
/// SQL Server instance via Testcontainers (docs task: "SKU attribute embedding
/// synchronization pipeline"). The schema is created by executing
/// deploy/sql/002_create_sku_attribute_catalog.sql directly against the
/// container, matching the convention already established by
/// SqlGoogleTaxonomyRepositoryTests.
/// </summary>
public sealed class SqlAttributeEmbeddingSourceRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private SqlAttributeEmbeddingSourceRepository _repository = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        _connectionString = _sqlContainer.GetConnectionString();

        await CreateSchemaAsync();

        var options = Options.Create(new GoogleTaxonomySqlOptions
        {
            ConnectionString = _connectionString
        });

        _repository = new SqlAttributeEmbeddingSourceRepository(options);
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    private async Task CreateSchemaAsync()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "deploy", "sql", "002_create_sku_attribute_catalog.sql");

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

    [Fact]
    public async Task GetActiveSearchableDefinitionsAsync_Should_Read_Active_Searchable_Definitions()
    {
        var definitions = await _repository.GetActiveSearchableDefinitionsAsync(CancellationToken.None);

        Assert.NotEmpty(definitions);
        Assert.All(definitions, d => Assert.True(d.IsActive));
        Assert.All(definitions, d => Assert.True(d.IsSearchable));

        var color = definitions.Single(d => d.Code == "color");
        Assert.Equal("Cor", color.Name);
        Assert.Equal("Text", color.DataType);
        Assert.True(color.IsVariantAxis);
    }

    [Fact]
    public async Task GetActiveSearchableDefinitionsAsync_Should_Ignore_Non_Searchable_Definitions()
    {
        var definitions = await _repository.GetActiveSearchableDefinitionsAsync(CancellationToken.None);

        // 'gtin' (id 1) is IsSearchable = 0 in the seed data.
        Assert.DoesNotContain(definitions, d => d.Code == "gtin");
    }

    [Fact]
    public async Task GetActiveSearchableDefinitionsAsync_Should_Map_Nullable_Fields_Correctly()
    {
        var definitions = await _repository.GetActiveSearchableDefinitionsAsync(CancellationToken.None);

        var color = definitions.Single(d => d.Code == "color");
        Assert.Null(color.UnitFamily);

        var weight = definitions.SingleOrDefault(d => d.Code == "product_weight");
        if (weight is not null)
        {
            Assert.Equal("Weight", weight.UnitFamily);
        }
    }

    [Fact]
    public async Task GetActiveOptionsAsync_Should_Read_Active_Options_With_Active_Definitions()
    {
        var options = await _repository.GetActiveOptionsAsync(CancellationToken.None);

        Assert.NotEmpty(options);
        Assert.All(options, o => Assert.True(o.IsActive));

        var maleOption = options.Single(o => o.AttributeCode == "gender" && o.OptionCode == "MALE");
        Assert.Equal("Masculino", maleOption.OptionName);
        Assert.Equal("male", maleOption.GoogleValue);
    }
}
