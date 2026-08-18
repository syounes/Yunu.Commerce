using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.CategoryResolution.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="IGoogleCategoryCatalogReader"/>
/// (docs task: "Google Category Resolution"). Batch-oriented: hydration
/// queries resolve many candidates in a single round-trip, mirroring <see
/// cref="Yunu.Commerce.Catalog.Infrastructure.AttributeCatalog.SqlServer.SqlAttributeCatalogReader"/>.
/// Uses plain ADO.NET (Microsoft.Data.SqlClient) against [Catalog].[GoogleTaxonomyCategories]
/// (deploy/databases/sqlserver/001-google-taxonomy-tables.sql), reusing the same Catalog SQL
/// Server connection (<see cref="GoogleTaxonomySqlOptions"/>,
/// "Catalog:GoogleTaxonomySql").
///
/// Comparisons use COLLATE Latin1_General_CI_AI so exact-match lookups are
/// case-insensitive and accent-insensitive, without altering the underlying
/// column collation.
/// </summary>
public sealed class SqlGoogleCategoryCatalogReader : IGoogleCategoryCatalogReader
{
    private const string CaseAccentInsensitiveCollation = "Latin1_General_CI_AI";

    private readonly string _connectionString;

    public SqlGoogleCategoryCatalogReader(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IReadOnlyList<GoogleCategoryCatalogEntry>> FindExactMatchesAsync(
        string categoryHint,
        string locale,
        CancellationToken cancellationToken)
    {
        var trimmedHint = categoryHint.Trim();

        if (trimmedHint.Length == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = $"""
            SELECT category.GoogleCategoryId, category.Name, category.FullPath, category.Level, category.IsLeaf, category.IsActive
            FROM [Catalog].[GoogleTaxonomyCategories] AS category
            WHERE category.IsActive = 1
              AND (
                category.Name COLLATE {CaseAccentInsensitiveCollation} = @Hint COLLATE {CaseAccentInsensitiveCollation}
                OR category.FullPath COLLATE {CaseAccentInsensitiveCollation} = @Hint COLLATE {CaseAccentInsensitiveCollation}
                OR (TRY_CONVERT(BIGINT, @Hint) IS NOT NULL AND category.GoogleCategoryId = TRY_CONVERT(BIGINT, @Hint))
              )
            """;

        command.Parameters.AddWithValue("@Hint", trimmedHint);

        return await ReadEntriesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<GoogleCategoryCatalogEntry>> GetByIdsAsync(
        IReadOnlyCollection<long> googleCategoryIds,
        CancellationToken cancellationToken)
    {
        if (googleCategoryIds.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var idsParameterNames = BuildInClauseParameters(command, "id", googleCategoryIds);

        command.CommandText = $"""
            SELECT category.GoogleCategoryId, category.Name, category.FullPath, category.Level, category.IsLeaf, category.IsActive
            FROM [Catalog].[GoogleTaxonomyCategories] AS category
            WHERE category.IsActive = 1
              AND category.GoogleCategoryId IN ({idsParameterNames})
            """;

        return await ReadEntriesAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<GoogleCategoryCatalogEntry>> ReadEntriesAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleCategoryCatalogEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GoogleCategoryCatalogEntry(
                GoogleCategoryId: reader.GetInt32(0),
                Name: reader.GetString(1),
                FullPath: reader.GetString(2),
                Level: reader.GetInt32(3),
                IsLeaf: reader.GetBoolean(4),
                IsActive: reader.GetBoolean(5)));
        }

        return results;
    }

    /// <summary>
    /// Adds one parameter per value to <paramref name="command"/> and returns
    /// the comma-separated list of parameter names, so callers can build a
    /// safe, fully parameterized <c>IN (...)</c> clause without concatenating
    /// raw values into SQL text.
    /// </summary>
    private static string BuildInClauseParameters(SqlCommand command, string prefix, IEnumerable<long> values)
    {
        var names = new List<string>();
        var index = 0;

        foreach (var value in values)
        {
            var parameterName = $"@{prefix}{index}";
            command.Parameters.AddWithValue(parameterName, value);
            names.Add(parameterName);
            index++;
        }

        return string.Join(", ", names);
    }
}
