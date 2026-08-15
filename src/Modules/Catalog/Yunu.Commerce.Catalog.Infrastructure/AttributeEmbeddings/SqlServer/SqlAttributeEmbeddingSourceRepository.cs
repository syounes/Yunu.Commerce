using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="IAttributeEmbeddingSourceRepository"/>
/// (docs task: "SKU attribute embedding synchronization pipeline"). Uses plain
/// ADO.NET (Microsoft.Data.SqlClient), matching the convention already
/// established by <see cref="Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer.SqlGoogleTaxonomyRepository"/>
/// and <see cref="Catalog.Infrastructure.AttributeCatalog.SqlServer.SqlAttributeCatalogRepository"/>:
/// no ORM, parameterized queries.
///
/// Reuses the existing Catalog SQL Server connection
/// (<see cref="GoogleTaxonomySqlOptions"/>, section "Catalog:GoogleTaxonomySql")
/// since Catalog.AttributeDefinitions / Catalog.AttributeOptions live in the
/// same database; no second SQL Server configuration section is introduced.
/// </summary>
public sealed class SqlAttributeEmbeddingSourceRepository : IAttributeEmbeddingSourceRepository
{
    private readonly string _connectionString;

    public SqlAttributeEmbeddingSourceRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IReadOnlyCollection<AttributeDefinitionSource>> GetActiveSearchableDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT AttributeDefinitionId, Code, GoogleAttributeName, Name, Description, SemanticText,
                   DataType, Cardinality, UnitFamily, ValidationRegex, MinNumericValue, MaxNumericValue,
                   MaxLength, IsGoogleMerchantAttribute, IsVariantAxis, IsSearchable, IsFilterable,
                   IsRequiredByDefault, DisplayOrder, IsActive, UpdatedAt
            FROM Catalog.AttributeDefinitions
            WHERE IsActive = 1
              AND IsSearchable = 1
            ORDER BY Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AttributeDefinitionSource>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AttributeDefinitionSource
            {
                AttributeDefinitionId = reader.GetInt32(0),
                Code = reader.GetString(1),
                GoogleAttributeName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Name = reader.GetString(3),
                Description = reader.GetString(4),
                SemanticText = reader.GetString(5),
                DataType = reader.GetString(6),
                Cardinality = reader.GetString(7),
                UnitFamily = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsGoogleMerchantAttribute = reader.GetBoolean(13),
                IsVariantAxis = reader.GetBoolean(14),
                IsSearchable = reader.GetBoolean(15),
                IsFilterable = reader.GetBoolean(16),
                IsRequiredByDefault = reader.GetBoolean(17),
                DisplayOrder = reader.GetInt16(18),
                IsActive = reader.GetBoolean(19),
                UpdatedAt = reader.GetDateTime(20)
            });
        }

        return results;
    }

    public async Task<IReadOnlyCollection<AttributeOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT o.AttributeOptionId, o.AttributeDefinitionId, d.Code, d.Name, o.Code, o.GoogleValue,
                   o.Name, o.SemanticText, o.DisplayOrder, o.IsActive
            FROM Catalog.AttributeOptions o
            INNER JOIN Catalog.AttributeDefinitions d ON d.AttributeDefinitionId = o.AttributeDefinitionId
            WHERE o.IsActive = 1
              AND d.IsActive = 1
            ORDER BY d.Code, o.Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AttributeOptionSource>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AttributeOptionSource
            {
                AttributeOptionId = reader.GetInt32(0),
                AttributeDefinitionId = reader.GetInt32(1),
                AttributeCode = reader.GetString(2),
                AttributeName = reader.GetString(3),
                OptionCode = reader.GetString(4),
                GoogleValue = reader.IsDBNull(5) ? null : reader.GetString(5),
                OptionName = reader.GetString(6),
                OptionSemanticText = reader.GetString(7),
                DisplayOrder = reader.GetInt16(8),
                IsActive = reader.GetBoolean(9)
            });
        }

        return results;
    }
}
