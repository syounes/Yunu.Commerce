using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

/// <summary>
/// SQL Server write adapter implementing <see cref="ISegmentDefinitionRepository"/>
/// over Catalog.SegmentDefinitions. Uses plain ADO.NET
/// (Microsoft.Data.SqlClient), matching the existing Brands/GoogleTaxonomy/
/// CanonicalTaxonomy adapters. Independent from the read-side
/// SqlSegmentCatalogRepository, which continues to serve
/// ISegmentCatalogRepository/GET endpoints unchanged.
/// </summary>
public sealed class SqlSegmentDefinitionRepository : ISegmentDefinitionRepository
{
    private readonly string _connectionString;

    public SqlSegmentDefinitionRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<SegmentDefinitionId> AddAsync(SegmentDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Id is not null)
        {
            throw new InvalidOperationException("Cannot add a SegmentDefinition that already has an identity.");
        }

        const string sql = """
            INSERT INTO Catalog.SegmentDefinitions
            (
                Code,
                Name,
                NormalizedName,
                Description,
                SemanticText,
                SelectionMode,
                AssignmentScope,
                Status
            )
            OUTPUT INSERTED.SegmentDefinitionId
            VALUES
            (
                @Code,
                @Name,
                @NormalizedName,
                @Description,
                @SemanticText,
                @SelectionMode,
                @AssignmentScope,
                @Status
            );
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", definition.Code.Value);
        command.Parameters.AddWithValue("@Name", definition.Name.Value);
        command.Parameters.AddWithValue("@NormalizedName", definition.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)definition.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@SemanticText", (object?)definition.SemanticText ?? DBNull.Value);
        command.Parameters.AddWithValue("@SelectionMode", definition.SelectionMode.ToString());
        command.Parameters.AddWithValue("@AssignmentScope", definition.AssignmentScope.ToString());
        command.Parameters.AddWithValue("@Status", definition.Status.ToString());

        var generatedId = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

        var segmentDefinitionId = new SegmentDefinitionId(generatedId);
        definition.AssignIdentity(segmentDefinitionId);

        return segmentDefinitionId;
    }

    public async Task UpdateAsync(SegmentDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Id is not { } id)
        {
            throw new InvalidOperationException("Cannot update a SegmentDefinition without an identity.");
        }

        const string sql = """
            UPDATE Catalog.SegmentDefinitions
            SET Name = @Name,
                NormalizedName = @NormalizedName,
                Description = @Description,
                SemanticText = @SemanticText,
                SelectionMode = @SelectionMode,
                AssignmentScope = @AssignmentScope,
                Status = @Status,
                UpdatedAt = SYSUTCDATETIME()
            WHERE SegmentDefinitionId = @SegmentDefinitionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", definition.Name.Value);
        command.Parameters.AddWithValue("@NormalizedName", definition.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)definition.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@SemanticText", (object?)definition.SemanticText ?? DBNull.Value);
        command.Parameters.AddWithValue("@SelectionMode", definition.SelectionMode.ToString());
        command.Parameters.AddWithValue("@AssignmentScope", definition.AssignmentScope.ToString());
        command.Parameters.AddWithValue("@Status", definition.Status.ToString());
        command.Parameters.AddWithValue("@SegmentDefinitionId", id.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"SegmentDefinition '{id.Value}' was not found.");
        }
    }

    public async Task<SegmentDefinition?> GetByIdAsync(SegmentDefinitionId id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, NormalizedName, Description, SemanticText, SelectionMode, AssignmentScope, Status
            FROM Catalog.SegmentDefinitions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", id.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadDefinition(reader);
    }

    public async Task<SegmentDefinition?> GetByCodeAsync(SegmentDefinitionCode code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, NormalizedName, Description, SemanticText, SelectionMode, AssignmentScope, Status
            FROM Catalog.SegmentDefinitions
            WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadDefinition(reader);
    }

    public async Task<SegmentDefinition?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, NormalizedName, Description, SemanticText, SelectionMode, AssignmentScope, Status
            FROM Catalog.SegmentDefinitions
            WHERE NormalizedName = @NormalizedName
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NormalizedName", normalizedName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadDefinition(reader);
    }

    public async Task<bool> ExistsCodeAsync(SegmentDefinitionCode code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM Catalog.SegmentDefinitions WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private static SegmentDefinition ReadDefinition(SqlDataReader reader)
    {
        var id = new SegmentDefinitionId(reader.GetInt64(0));
        var code = new SegmentDefinitionCode(reader.GetString(1));
        var name = new SegmentDefinitionName(reader.GetString(2));
        var normalizedName = reader.GetString(3);
        var description = reader.IsDBNull(4) ? null : reader.GetString(4);
        var semanticText = reader.IsDBNull(5) ? null : reader.GetString(5);
        var selectionMode = Enum.Parse<SegmentSelectionMode>(reader.GetString(6), ignoreCase: true);
        var assignmentScope = Enum.Parse<SegmentAssignmentScope>(reader.GetString(7), ignoreCase: true);
        var status = Enum.Parse<SegmentDefinitionStatus>(reader.GetString(8), ignoreCase: true);

        return SegmentDefinition.Hydrate(
            id,
            code,
            name,
            normalizedName,
            description,
            semanticText,
            selectionMode,
            assignmentScope,
            status);
    }
}
