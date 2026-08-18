using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.AttributeCatalog;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.AttributeCatalog.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="IAttributeCatalogRepository"/>
/// (docs task: "SKU attribute foundation"). Uses plain ADO.NET
/// (Microsoft.Data.SqlClient), matching the convention already established by
/// <see cref="Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer.SqlGoogleTaxonomyRepository"/>:
/// no ORM, parameterized queries, single-row lookups only (never loads the
/// entire attribute catalog to resolve one attribute).
///
/// Reuses the existing Catalog SQL Server connection
/// (<see cref="GoogleTaxonomySqlOptions"/>, section "Catalog:GoogleTaxonomySql")
/// since Catalog.AttributeDefinitions / Catalog.AttributeOptions live in the
/// same database as Catalog.GoogleTaxonomyCategories
/// (deploy/databases/sqlserver/002_create_sku_attribute_catalog.sql); no second SQL Server
/// configuration section is introduced.
/// </summary>
public sealed class SqlAttributeCatalogRepository : IAttributeCatalogRepository
{
    private readonly string _connectionString;

    public SqlAttributeCatalogRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<AttributeDefinitionResponse?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT AttributeDefinitionId, Code, Name, DataType, Cardinality, UnitFamily,
                   ValidationRegex, MinNumericValue, MaxNumericValue, MaxLength,
                   IsVariantAxis, IsSearchable, IsFilterable, IsActive
            FROM Catalog.AttributeDefinitions
            WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadDefinition(reader) : null;
    }

    public async Task<AttributeDefinitionResponse?> GetDefinitionByIdAsync(int attributeDefinitionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT AttributeDefinitionId, Code, Name, DataType, Cardinality, UnitFamily,
                   ValidationRegex, MinNumericValue, MaxNumericValue, MaxLength,
                   IsVariantAxis, IsSearchable, IsFilterable, IsActive
            FROM Catalog.AttributeDefinitions
            WHERE AttributeDefinitionId = @AttributeDefinitionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AttributeDefinitionId", attributeDefinitionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadDefinition(reader) : null;
    }

    public async Task<AttributeOptionResponse?> GetOptionAsync(int attributeDefinitionId, string optionCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT AttributeOptionId, AttributeDefinitionId, Code, GoogleValue, Name, IsActive
            FROM Catalog.AttributeOptions
            WHERE AttributeDefinitionId = @AttributeDefinitionId
              AND Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@AttributeDefinitionId", attributeDefinitionId);
        command.Parameters.AddWithValue("@Code", optionCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AttributeOptionResponse
        {
            AttributeOptionId = reader.GetInt32(0),
            AttributeDefinitionId = reader.GetInt32(1),
            Code = reader.GetString(2),
            GoogleValue = reader.IsDBNull(3) ? null : reader.GetString(3),
            Name = reader.GetString(4),
            IsActive = reader.GetBoolean(5)
        };
    }

    private static AttributeDefinitionResponse ReadDefinition(SqlDataReader reader)
    {
        return new AttributeDefinitionResponse
        {
            AttributeDefinitionId = reader.GetInt32(0),
            Code = reader.GetString(1),
            Name = reader.GetString(2),
            DataType = reader.GetString(3),
            Cardinality = reader.GetString(4),
            UnitFamily = reader.IsDBNull(5) ? null : reader.GetString(5),
            ValidationRegex = reader.IsDBNull(6) ? null : reader.GetString(6),
            MinNumericValue = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
            MaxNumericValue = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
            MaxLength = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            IsVariantAxis = reader.GetBoolean(10),
            IsSearchable = reader.GetBoolean(11),
            IsFilterable = reader.GetBoolean(12),
            IsActive = reader.GetBoolean(13)
        };
    }
}
