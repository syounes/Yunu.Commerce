using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ISegmentCatalogRepository"/>
/// (docs task: "Canonical Taxonomy + Segments Domain" §23-§24). Read-only
/// reference data adapter over Catalog.SegmentDefinitions and
/// Catalog.SegmentOptions
/// (deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql,
/// deploy/databases/sqlserver/008-add-segment-assignment-scope.sql). Uses plain
/// ADO.NET (Microsoft.Data.SqlClient), matching the existing Brands/GoogleTaxonomy
/// adapters.
/// </summary>
public sealed class SqlSegmentCatalogRepository : ISegmentCatalogRepository
{
    private readonly string _connectionString;

    public SqlSegmentCatalogRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<SegmentDefinitionResponse?> GetDefinitionByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, Description, SemanticText, SelectionMode, AssignmentScope, IsRequired, Status
            FROM Catalog.SegmentDefinitions
            WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadDefinition(reader);
    }

    public async Task<SegmentDefinitionResponse?> GetDefinitionByIdAsync(long segmentDefinitionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, Description, SemanticText, SelectionMode, AssignmentScope, IsRequired, Status
            FROM Catalog.SegmentDefinitions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadDefinition(reader);
    }

    public async Task<IReadOnlyCollection<SegmentDefinitionResponse>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, Description, SemanticText, SelectionMode, AssignmentScope, IsRequired, Status
            FROM Catalog.SegmentDefinitions
            ORDER BY Name, Code, SegmentDefinitionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SegmentDefinitionResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadDefinition(reader));
        }

        return results;
    }

    public async Task<SegmentOptionResponse?> GetOptionAsync(long segmentDefinitionId, string optionCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentOptionId, SegmentDefinitionId, Code, Name, Description, DisplayOrder, Status
            FROM Catalog.SegmentOptions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
              AND Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId);
        command.Parameters.AddWithValue("@Code", optionCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadOption(reader);
    }

    public async Task<IReadOnlyCollection<SegmentOptionResponse>> GetOptionsByDefinitionAsync(long segmentDefinitionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentOptionId, SegmentDefinitionId, Code, Name, Description, DisplayOrder, Status
            FROM Catalog.SegmentOptions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
            ORDER BY DisplayOrder, SegmentOptionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SegmentOptionResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadOption(reader));
        }

        return results;
    }

    private static SegmentDefinitionResponse ReadDefinition(SqlDataReader reader)
    {
        return new SegmentDefinitionResponse
        {
            SegmentDefinitionId = reader.GetInt64(0),
            Code = reader.GetString(1),
            Name = reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            SemanticText = reader.IsDBNull(4) ? null : reader.GetString(4),
            SelectionMode = reader.GetString(5),
            AssignmentScope = reader.GetString(6),
            IsRequired = reader.GetBoolean(7),
            Status = reader.GetString(8)
        };
    }

    private static SegmentOptionResponse ReadOption(SqlDataReader reader)
    {
        return new SegmentOptionResponse
        {
            SegmentOptionId = reader.GetInt64(0),
            SegmentDefinitionId = reader.GetInt64(1),
            Code = reader.GetString(2),
            Name = reader.GetString(3),
            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
            DisplayOrder = reader.GetInt32(5),
            Status = reader.GetString(6)
        };
    }
}
