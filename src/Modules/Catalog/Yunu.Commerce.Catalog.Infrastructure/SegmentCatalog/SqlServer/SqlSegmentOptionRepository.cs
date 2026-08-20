using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

/// <summary>
/// SQL Server write adapter implementing <see cref="ISegmentOptionRepository"/>
/// over Catalog.SegmentOptions. Uses plain ADO.NET (Microsoft.Data.SqlClient),
/// matching <see cref="SqlSegmentDefinitionRepository"/>. Independent from
/// the read-side SqlSegmentCatalogRepository, which continues to serve
/// ISegmentCatalogRepository/GET endpoints unchanged.
/// </summary>
public sealed class SqlSegmentOptionRepository : ISegmentOptionRepository
{
    private readonly string _connectionString;

    public SqlSegmentOptionRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<SegmentOptionId> AddAsync(SegmentOption option, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (option.Id is not null)
        {
            throw new InvalidOperationException("Cannot add a SegmentOption that already has an identity.");
        }

        const string sql = """
            INSERT INTO Catalog.SegmentOptions
            (
                SegmentDefinitionId,
                Code,
                Name,
                NormalizedName,
                Description,
                SemanticText,
                DisplayOrder,
                Status
            )
            OUTPUT INSERTED.SegmentOptionId
            VALUES
            (
                @SegmentDefinitionId,
                @Code,
                @Name,
                @NormalizedName,
                @Description,
                @SemanticText,
                @DisplayOrder,
                @Status
            );
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", option.SegmentDefinitionId.Value);
        command.Parameters.AddWithValue("@Code", option.Code.Value);
        command.Parameters.AddWithValue("@Name", option.Name.Value);
        command.Parameters.AddWithValue("@NormalizedName", option.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)option.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@SemanticText", (object?)option.SemanticText ?? DBNull.Value);
        command.Parameters.AddWithValue("@DisplayOrder", option.DisplayOrder);
        command.Parameters.AddWithValue("@Status", option.Status.ToString());

        var generatedId = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

        var segmentOptionId = new SegmentOptionId(generatedId);
        option.AssignIdentity(segmentOptionId);

        return segmentOptionId;
    }

    public async Task UpdateAsync(SegmentOption option, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(option);

        if (option.Id is not { } id)
        {
            throw new InvalidOperationException("Cannot update a SegmentOption without an identity.");
        }

        const string sql = """
            UPDATE Catalog.SegmentOptions
            SET Name = @Name,
                NormalizedName = @NormalizedName,
                Description = @Description,
                SemanticText = @SemanticText,
                DisplayOrder = @DisplayOrder,
                Status = @Status,
                UpdatedAt = SYSUTCDATETIME()
            WHERE SegmentOptionId = @SegmentOptionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", option.Name.Value);
        command.Parameters.AddWithValue("@NormalizedName", option.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)option.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@SemanticText", (object?)option.SemanticText ?? DBNull.Value);
        command.Parameters.AddWithValue("@DisplayOrder", option.DisplayOrder);
        command.Parameters.AddWithValue("@Status", option.Status.ToString());
        command.Parameters.AddWithValue("@SegmentOptionId", id.Value);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"SegmentOption '{id.Value}' was not found.");
        }
    }

    public async Task<SegmentOption?> GetByIdAsync(SegmentOptionId id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentOptionId, SegmentDefinitionId, Code, Name, NormalizedName, Description, SemanticText, DisplayOrder, Status
            FROM Catalog.SegmentOptions
            WHERE SegmentOptionId = @SegmentOptionId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentOptionId", id.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadOption(reader);
    }

    public async Task<SegmentOption?> GetByCodeAsync(SegmentDefinitionId segmentDefinitionId, SegmentOptionCode code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentOptionId, SegmentDefinitionId, Code, Name, NormalizedName, Description, SemanticText, DisplayOrder, Status
            FROM Catalog.SegmentOptions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
              AND Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId.Value);
        command.Parameters.AddWithValue("@Code", code.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadOption(reader);
    }

    public async Task<SegmentOption?> FindByNormalizedNameAsync(SegmentDefinitionId segmentDefinitionId, string normalizedName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SegmentOptionId, SegmentDefinitionId, Code, Name, NormalizedName, Description, SemanticText, DisplayOrder, Status
            FROM Catalog.SegmentOptions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
              AND NormalizedName = @NormalizedName
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId.Value);
        command.Parameters.AddWithValue("@NormalizedName", normalizedName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadOption(reader);
    }

    public async Task<bool> ExistsCodeAsync(SegmentDefinitionId segmentDefinitionId, SegmentOptionCode code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM Catalog.SegmentOptions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
              AND Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId.Value);
        command.Parameters.AddWithValue("@Code", code.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private static SegmentOption ReadOption(SqlDataReader reader)
    {
        var id = new SegmentOptionId(reader.GetInt64(0));
        var segmentDefinitionId = new SegmentDefinitionId(reader.GetInt64(1));
        var code = new SegmentOptionCode(reader.GetString(2));
        var name = new SegmentOptionName(reader.GetString(3));
        var normalizedName = reader.GetString(4);
        var description = reader.IsDBNull(5) ? null : reader.GetString(5);
        var semanticText = reader.IsDBNull(6) ? null : reader.GetString(6);
        var displayOrder = reader.GetInt32(7);
        var status = Enum.Parse<SegmentOptionStatus>(reader.GetString(8));

        return SegmentOption.Hydrate(
            id,
            segmentDefinitionId,
            code,
            name,
            normalizedName,
            description,
            semanticText,
            displayOrder,
            status);
    }
}
