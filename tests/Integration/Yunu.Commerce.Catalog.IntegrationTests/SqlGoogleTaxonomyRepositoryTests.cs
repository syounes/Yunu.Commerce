using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for SqlGoogleTaxonomyRepository against a real SQL Server
/// instance via Testcontainers (docs task: "Create SQL migration/setup scripts").
/// The schema is created by executing deploy/sql/001-google-taxonomy-tables.sql
/// directly against the container, matching the repository's own conventions
/// (no ORM/migration framework is introduced for this feature).
/// </summary>
public sealed class SqlGoogleTaxonomyRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();
    private SqlGoogleTaxonomyRepository _repository = null!;
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

        _repository = new SqlGoogleTaxonomyRepository(options);
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
            "deploy", "sql", "001-google-taxonomy-tables.sql");

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

    private static GoogleTaxonomyCategoryItem[] SampleCategories() => new[]
    {
        new GoogleTaxonomyCategoryItem(166, null, "Apparel & Accessories", "Apparel & Accessories", 0, false),
        new GoogleTaxonomyCategoryItem(187, 166, "Shoes", "Apparel & Accessories > Shoes", 1, true)
    };

    [Fact]
    public async Task SynchronizeAsync_FirstImport_Should_Insert_All_Categories()
    {
        var result = await _repository.SynchronizeAsync(SampleCategories(), "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(2, result.TotalCategories);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Deactivated);
    }

    [Fact]
    public async Task SynchronizeAsync_SecondIdenticalImport_Should_Be_Idempotent()
    {
        var categories = SampleCategories();

        await _repository.SynchronizeAsync(categories, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);
        var second = await _repository.SynchronizeAsync(categories, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Updated);
        Assert.Equal(0, second.Deactivated);
    }

    [Fact]
    public async Task SynchronizeAsync_With_ChangedCategoryName_Should_Update_Record()
    {
        var categories = SampleCategories();

        await _repository.SynchronizeAsync(categories, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        var changed = new[]
        {
            categories[0],
            categories[1] with { Name = "Athletic Shoes" }
        };

        var result = await _repository.SynchronizeAsync(changed, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.Updated);

        var updated = await _repository.GetByIdAsync(187, CancellationToken.None);
        Assert.Equal("Athletic Shoes", updated!.Name);
    }

    [Fact]
    public async Task SynchronizeAsync_With_RemovedCategory_Should_Deactivate_It()
    {
        var categories = SampleCategories();

        await _repository.SynchronizeAsync(categories, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        var reduced = new[] { categories[0] };

        var result = await _repository.SynchronizeAsync(reduced, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, result.Deactivated);

        var deactivated = await _repository.GetByIdAsync(187, CancellationToken.None);
        Assert.False(deactivated!.IsActive);
    }

    [Fact]
    public async Task SynchronizeAsync_With_ReappearingCategory_Should_Reactivate_It()
    {
        var categories = SampleCategories();

        await _repository.SynchronizeAsync(categories, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);
        await _repository.SynchronizeAsync(new[] { categories[0] }, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        var reactivateResult = await _repository.SynchronizeAsync(categories, "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, reactivateResult.Updated);

        var reactivated = await _repository.GetByIdAsync(187, CancellationToken.None);
        Assert.True(reactivated!.IsActive);
    }

    [Fact]
    public async Task SynchronizeAsync_Should_Persist_ParentChild_Relationship()
    {
        await _repository.SynchronizeAsync(SampleCategories(), "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        var ancestors = await _repository.GetAncestorsAsync(187, CancellationToken.None);

        Assert.Single(ancestors);
        Assert.Equal(166, ancestors.First().GoogleCategoryId);
    }

    [Fact]
    public async Task SynchronizeAsync_Should_Record_Import_History()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await _repository.SynchronizeAsync(SampleCategories(), "en-US", "https://example.com/taxonomy.txt", DateTime.UtcNow, CancellationToken.None);

        await using var command = new SqlCommand("SELECT COUNT(*) FROM GoogleTaxonomyImports WHERE Status = 'Completed'", connection);
        var count = (int)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, count);
    }
}
