using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.AttributeCatalog.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="IAttributeCatalogReader"/>
/// (docs task: "Semantic attribute hint resolution"). Batch-oriented: exact
/// match and hydration queries resolve many candidates in a single
/// round-trip, instead of one query per hint/candidate (docs §12,
/// performance). Uses plain ADO.NET (Microsoft.Data.SqlClient), mirroring
/// <see cref="Yunu.Commerce.Catalog.Infrastructure.AttributeCatalog.SqlServer.SqlAttributeCatalogRepository"/>
/// and reusing the same Catalog SQL Server connection
/// (<see cref="GoogleTaxonomySqlOptions"/>, "Catalog:GoogleTaxonomySql")
/// since Catalog.AttributeDefinitions / Catalog.AttributeOptions /
/// Catalog.GoogleCategoryAttributeRules all live in that database.
///
/// Comparisons use COLLATE Latin1_General_CI_AI so exact-match lookups are
/// case-insensitive and accent-insensitive (e.g. "publico" matches
/// "público"), without altering the underlying column collation.
/// </summary>
public sealed class SqlAttributeCatalogReader : IAttributeCatalogReader
{
    private const string CaseAccentInsensitiveCollation = "Latin1_General_CI_AI";

    private readonly string _connectionString;

    public SqlAttributeCatalogReader(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> FindDefinitionsByExactMatchAsync(
        IReadOnlyCollection<string> normalizedValues,
        CancellationToken cancellationToken)
    {
        if (normalizedValues.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var valuesParameterNames = BuildInClauseParameters(command, "v", normalizedValues);

        command.CommandText = $"""
            SELECT AttributeDefinitionId, Code, Name, GoogleAttributeName, DataType, Cardinality, UnitFamily,
                   ValidationRegex, MinNumericValue, MaxNumericValue, MaxLength, IsActive
            FROM Catalog.AttributeDefinitions
            WHERE IsActive = 1
              AND (
                Code COLLATE {CaseAccentInsensitiveCollation} IN ({valuesParameterNames})
                OR Name COLLATE {CaseAccentInsensitiveCollation} IN ({valuesParameterNames})
                OR GoogleAttributeName COLLATE {CaseAccentInsensitiveCollation} IN ({valuesParameterNames})
              )
            """;

        return await ReadDefinitionsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> GetDefinitionsByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var codesParameterNames = BuildInClauseParameters(command, "c", codes);

        command.CommandText = $"""
            SELECT AttributeDefinitionId, Code, Name, GoogleAttributeName, DataType, Cardinality, UnitFamily,
                   ValidationRegex, MinNumericValue, MaxNumericValue, MaxLength, IsActive
            FROM Catalog.AttributeDefinitions
            WHERE IsActive = 1
              AND Code IN ({codesParameterNames})
            """;

        return await ReadDefinitionsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeOptionCatalogEntry>> FindOptionsByExactMatchAsync(
        int attributeDefinitionId,
        IReadOnlyCollection<string> normalizedValues,
        CancellationToken cancellationToken)
    {
        if (normalizedValues.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var valuesParameterNames = BuildInClauseParameters(command, "v", normalizedValues);
        command.Parameters.AddWithValue("@AttributeDefinitionId", attributeDefinitionId);

        command.CommandText = $"""
            SELECT AttributeOptionId, AttributeDefinitionId, Code, GoogleValue, Name, IsActive
            FROM Catalog.AttributeOptions
            WHERE IsActive = 1
              AND AttributeDefinitionId = @AttributeDefinitionId
              AND (
                Code COLLATE {CaseAccentInsensitiveCollation} IN ({valuesParameterNames})
                OR Name COLLATE {CaseAccentInsensitiveCollation} IN ({valuesParameterNames})
                OR GoogleValue COLLATE {CaseAccentInsensitiveCollation} IN ({valuesParameterNames})
              )
            """;

        return await ReadOptionsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeOptionCatalogEntry>> GetOptionsByCodesAsync(
        int attributeDefinitionId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var codesParameterNames = BuildInClauseParameters(command, "c", codes);
        command.Parameters.AddWithValue("@AttributeDefinitionId", attributeDefinitionId);

        command.CommandText = $"""
            SELECT AttributeOptionId, AttributeDefinitionId, Code, GoogleValue, Name, IsActive
            FROM Catalog.AttributeOptions
            WHERE IsActive = 1
              AND AttributeDefinitionId = @AttributeDefinitionId
              AND Code IN ({codesParameterNames})
            """;

        return await ReadOptionsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<GoogleCategoryAttributeRuleEntry>> GetCategoryRulesAsync(
        long googleCategoryId,
        IReadOnlyCollection<int> attributeDefinitionIds,
        CancellationToken cancellationToken)
    {
        if (attributeDefinitionIds.Count == 0)
        {
            return [];
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var idsParameterNames = BuildInClauseParameters(command, "id", attributeDefinitionIds.Select(id => id.ToString()));
        command.Parameters.AddWithValue("@GoogleCategoryId", googleCategoryId);

        command.CommandText = $"""
            SELECT GoogleCategoryId, AttributeDefinitionId, RequirementLevel, IsVariantAxis
            FROM Catalog.GoogleCategoryAttributeRules
            WHERE GoogleCategoryId = @GoogleCategoryId
              AND AttributeDefinitionId IN ({idsParameterNames})
              AND CountryCode = '*'
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleCategoryAttributeRuleEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GoogleCategoryAttributeRuleEntry(
                GoogleCategoryId: reader.GetInt64(0),
                AttributeDefinitionId: reader.GetInt32(1),
                RequirementLevel: reader.GetString(2),
                IsVariantAxis: reader.GetBoolean(3)));
        }

        return results;
    }

    private static async Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> ReadDefinitionsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AttributeDefinitionCatalogEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AttributeDefinitionCatalogEntry(
                AttributeDefinitionId: reader.GetInt32(0),
                Code: reader.GetString(1),
                Name: reader.GetString(2),
                GoogleAttributeName: reader.IsDBNull(3) ? null : reader.GetString(3),
                DataType: reader.GetString(4),
                Cardinality: reader.GetString(5),
                UnitFamily: reader.IsDBNull(6) ? null : reader.GetString(6),
                ValidationRegex: reader.IsDBNull(7) ? null : reader.GetString(7),
                MinNumericValue: reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                MaxNumericValue: reader.IsDBNull(9) ? null : reader.GetDecimal(9),
                MaxLength: reader.IsDBNull(10) ? null : reader.GetInt32(10),
                IsActive: reader.GetBoolean(11)));
        }

        return results;
    }

    private static async Task<IReadOnlyList<AttributeOptionCatalogEntry>> ReadOptionsAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AttributeOptionCatalogEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AttributeOptionCatalogEntry(
                AttributeOptionId: reader.GetInt32(0),
                AttributeDefinitionId: reader.GetInt32(1),
                Code: reader.GetString(2),
                GoogleValue: reader.IsDBNull(3) ? null : reader.GetString(3),
                Name: reader.GetString(4),
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
    private static string BuildInClauseParameters(SqlCommand command, string prefix, IEnumerable<string> values)
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
