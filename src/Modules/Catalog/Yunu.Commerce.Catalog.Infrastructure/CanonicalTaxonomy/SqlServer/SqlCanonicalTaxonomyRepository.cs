using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ICanonicalTaxonomyRepository"/>
/// (docs task: "Canonical Taxonomy + Segments Domain" §4, §19-§22). Uses plain
/// ADO.NET (Microsoft.Data.SqlClient), matching the existing Brands/GoogleTaxonomy
/// adapters. Persists to Catalog.CanonicalTaxonomyNodes
/// (deploy/databases/sqlserver/009-reset-canonical-taxonomy-starter.sql).
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
                (ParentId, Code, Name, NormalizedName, Description, Depth, Path, GoogleCategoryId, Source, Status)
            OUTPUT INSERTED.CanonicalTaxonomyNodeId
            VALUES
                (@ParentId, @Code, @Name, @NormalizedName, @Description, @Depth, @Path, @GoogleCategoryId, @Source, @Status)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ParentId", node.ParentId is { } parentId ? parentId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Code", node.Code);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@NormalizedName", node.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)node.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@Depth", node.Depth);
        command.Parameters.AddWithValue("@Path", node.Path);
        command.Parameters.AddWithValue("@GoogleCategoryId", (object?)node.GoogleCategoryId ?? DBNull.Value);
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
                Path = @Path,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE CanonicalTaxonomyNodeId = @CanonicalTaxonomyNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@NormalizedName", node.NormalizedName);
        command.Parameters.AddWithValue("@Description", (object?)node.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@Path", node.Path);
        command.Parameters.AddWithValue("@Status", node.Status.ToString());
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@CanonicalTaxonomyNodeId", node.Id.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CanonicalTaxonomyNode?> GetByIdAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CanonicalTaxonomyNodeId, ParentId, Code, Name, NormalizedName, Description, Depth, Path, GoogleCategoryId, Source, Status
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
            SELECT CanonicalTaxonomyNodeId, ParentId, Code, Name, NormalizedName, Description, Depth, Path, GoogleCategoryId, Source, Status
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

    public async Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetRootsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CanonicalTaxonomyNodeId, ParentId, Code, Name, NormalizedName, Description, Depth, Path, GoogleCategoryId, Source, Status
            FROM Catalog.CanonicalTaxonomyNodes
            WHERE ParentId IS NULL
            ORDER BY Path, CanonicalTaxonomyNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
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
        var code = reader.GetString(2);
        var name = reader.GetString(3);
        var normalizedName = reader.GetString(4);
        var description = reader.IsDBNull(5) ? null : reader.GetString(5);
        var depth = reader.GetInt16(6);
        var path = reader.GetString(7);
        var googleCategoryId = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8);
        var source = Enum.Parse<CanonicalTaxonomySource>(reader.GetString(9));
        var status = Enum.Parse<CanonicalTaxonomyNodeStatus>(reader.GetString(10));

        return CanonicalTaxonomyNode.Hydrate(
            id, parentId, googleCategoryId, code, name, normalizedName, description, depth, path, source, status);
    }
}
