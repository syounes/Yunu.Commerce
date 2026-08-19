using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ICanonicalTaxonomyRepository"/>
/// (docs task: "Canonical Taxonomy + Segments Domain" §4, §19-§22). Uses plain
/// ADO.NET (Microsoft.Data.SqlClient), matching the existing Brands/GoogleTaxonomy
/// adapters. Persists to Catalog.CanonicalTaxonomyNodes
/// (deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql).
/// </summary>
public sealed class SqlCanonicalTaxonomyRepository : ICanonicalTaxonomyRepository
{
    private readonly string _connectionString;

    public SqlCanonicalTaxonomyRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<CanonicalTaxonomyNodeId> AddAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Catalog.CanonicalTaxonomyNodes
                (ParentId, SegmentDefinitionId, Code, Name, NormalizedName, Description, Depth, Path, Source, Status)
            OUTPUT INSERTED.CanonicalTaxonomyNodeId
            VALUES
                (@ParentId, @SegmentDefinitionId, @Code, @Name, @NormalizedName, @Description, @Depth, @Path, @Source, @Status)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ParentId", node.ParentId is { } parentId ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@SegmentDefinitionId", node.SegmentDefinitionId is { } segmentDefinitionId ? segmentDefinitionId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Code", node.Code);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@NormalizedName", node.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)node.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@Depth", node.Depth);
        command.Parameters.AddWithValue("@Path", node.Path);
        command.Parameters.AddWithValue("@Source", node.Source.ToString());
        command.Parameters.AddWithValue("@Status", node.Status.ToString());

        var generatedId = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

        return new CanonicalTaxonomyNodeId(generatedId);
    }

    public async Task UpdateAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Catalog.CanonicalTaxonomyNodes
            SET Name = @Name,
                NormalizedName = @NormalizedName,
                Description = @Description,
                UpdatedAt = @UpdatedAt
            WHERE CanonicalTaxonomyNodeId = @CanonicalTaxonomyNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@NormalizedName", node.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)node.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@CanonicalTaxonomyNodeId", node.Id.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM Catalog.CanonicalTaxonomyNodes
            WHERE CanonicalTaxonomyNodeId = @CanonicalTaxonomyNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CanonicalTaxonomyNodeId", id.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CanonicalTaxonomyNode?> GetByIdAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CanonicalTaxonomyNodeId, ParentId, SegmentDefinitionId, Code, Name, NormalizedName, Description, Depth, Path, Source, Status
            FROM Catalog.CanonicalTaxonomyNodes
            WHERE CanonicalTaxonomyNodeId = @CanonicalTaxonomyNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CanonicalTaxonomyNodeId", id.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadNode(reader);
    }

    public async Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetChildrenAsync(CanonicalTaxonomyNodeId parentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CanonicalTaxonomyNodeId, ParentId, SegmentDefinitionId, Code, Name, NormalizedName, Description, Depth, Path, Source, Status
            FROM Catalog.CanonicalTaxonomyNodes
            WHERE ParentId = @ParentId
            ORDER BY Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ParentId", parentId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<CanonicalTaxonomyNode>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadNode(reader));
        }

        return results;
    }

    public async Task<bool> HasChildrenAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) 1
            FROM Catalog.CanonicalTaxonomyNodes
            WHERE ParentId = @ParentId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ParentId", id.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private static CanonicalTaxonomyNode ReadNode(SqlDataReader reader)
    {
        var id = new CanonicalTaxonomyNodeId(reader.GetInt64(0));
        var parentId = reader.IsDBNull(1) ? (CanonicalTaxonomyNodeId?)null : new CanonicalTaxonomyNodeId(reader.GetInt64(1));
        var segmentDefinitionId = reader.IsDBNull(2) ? (SegmentDefinitionId?)null : new SegmentDefinitionId(reader.GetInt64(2));
        var code = reader.GetString(3);
        var name = reader.GetString(4);
        var normalizedName = reader.GetString(5);
        var description = reader.IsDBNull(6) ? null : reader.GetString(6);
        var depth = reader.GetInt16(7);
        var path = reader.GetString(8);
        var source = Enum.Parse<CanonicalTaxonomySource>(reader.GetString(9));
        var status = Enum.Parse<CanonicalTaxonomyNodeStatus>(reader.GetString(10));

        return CanonicalTaxonomyNode.Hydrate(
            id, parentId, segmentDefinitionId, code, name, normalizedName, description, depth, path, source, status);
    }
}
