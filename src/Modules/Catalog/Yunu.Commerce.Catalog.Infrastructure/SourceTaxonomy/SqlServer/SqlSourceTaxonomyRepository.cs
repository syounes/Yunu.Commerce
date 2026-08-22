using Microsoft.Data.SqlClient;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy;

namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ISourceTaxonomyRepository"/>
/// (docs/adr/0014-provider-neutral-source-taxonomy.md). Uses plain ADO.NET
/// (Microsoft.Data.SqlClient), matching the existing Brands/GoogleTaxonomy/
/// CanonicalTaxonomy adapters. Persists to Catalog.SourceTaxonomies and
/// Catalog.SourceTaxonomyNodes
/// (deploy/databases/sqlserver/014-create-source-taxonomy-foundation.sql).
///
/// This is Phase 2 scope only: simple internal create/read behavior. No
/// provider adapter, import orchestration, upsert or deactivation logic is
/// implemented here; those belong to a later phase.
///
/// This class depends only on a plain SQL connection string, not on any
/// Google-specific (or other provider-specific) configuration type. The
/// existing shared Catalog SQL connection source naming debt
/// (GoogleTaxonomySqlOptions) is isolated to the composition root
/// (CatalogInfrastructureServiceCollectionExtensions).
/// </summary>
public sealed class SqlSourceTaxonomyRepository : ISourceTaxonomyRepository
{
    private readonly string _connectionString;

    public SqlSourceTaxonomyRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<long> CreateAsync(SourceTaxonomyCreateRecord source, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Catalog.SourceTaxonomies
                (Code, Name, ProviderCode, ScopeCode, ExternalTaxonomyId, ExternalVersion,
                 DefaultLanguage, SourceUri, SourceChecksum, IsActive, ImportedAt)
            OUTPUT INSERTED.SourceTaxonomyId
            VALUES
                (@Code, @Name, @ProviderCode, @ScopeCode, @ExternalTaxonomyId, @ExternalVersion,
                 @DefaultLanguage, @SourceUri, @SourceChecksum, @IsActive, @ImportedAt)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", source.Code);
        command.Parameters.AddWithValue("@Name", source.Name);
        command.Parameters.AddWithValue("@ProviderCode", source.ProviderCode);
        command.Parameters.AddWithValue("@ScopeCode", (object?)source.ScopeCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExternalTaxonomyId", (object?)source.ExternalTaxonomyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExternalVersion", (object?)source.ExternalVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("@DefaultLanguage", source.DefaultLanguage);
        command.Parameters.AddWithValue("@SourceUri", (object?)source.SourceUri ?? DBNull.Value);
        command.Parameters.AddWithValue("@SourceChecksum", (object?)source.SourceChecksum ?? DBNull.Value);
        command.Parameters.AddWithValue("@IsActive", source.IsActive);
        command.Parameters.AddWithValue("@ImportedAt", source.ImportedAt);

        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<SourceTaxonomyDescriptorRecord?> GetByIdAsync(long sourceTaxonomyId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyId, Code, Name, ProviderCode, ScopeCode, ExternalTaxonomyId,
                   ExternalVersion, DefaultLanguage, SourceUri, SourceChecksum, IsActive,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomies
            WHERE SourceTaxonomyId = @SourceTaxonomyId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadDescriptor(reader);
    }

    public async Task<SourceTaxonomyDescriptorRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyId, Code, Name, ProviderCode, ScopeCode, ExternalTaxonomyId,
                   ExternalVersion, DefaultLanguage, SourceUri, SourceChecksum, IsActive,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomies
            WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadDescriptor(reader);
    }

    public async Task<IReadOnlyCollection<SourceTaxonomyDescriptorRecord>> GetActiveAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyId, Code, Name, ProviderCode, ScopeCode, ExternalTaxonomyId,
                   ExternalVersion, DefaultLanguage, SourceUri, SourceChecksum, IsActive,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomies
            WHERE IsActive = 1
            ORDER BY Code, SourceTaxonomyId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SourceTaxonomyDescriptorRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadDescriptor(reader));
        }

        return results;
    }

    public async Task<long> CreateNodeAsync(SourceTaxonomyNodeCreateRecord node, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Catalog.SourceTaxonomyNodes
                (SourceTaxonomyId, ExternalNodeId, ParentSourceTaxonomyNodeId, NodeType, Name,
                 FullPath, Level, IsLeaf, IsActive, SourceLanguage, ImportedAt)
            OUTPUT INSERTED.SourceTaxonomyNodeId
            VALUES
                (@SourceTaxonomyId, @ExternalNodeId, @ParentSourceTaxonomyNodeId, @NodeType, @Name,
                 @FullPath, @Level, @IsLeaf, @IsActive, @SourceLanguage, @ImportedAt)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", node.SourceTaxonomyId);
        command.Parameters.AddWithValue("@ExternalNodeId", node.ExternalNodeId);
        command.Parameters.AddWithValue("@ParentSourceTaxonomyNodeId", (object?)node.ParentSourceTaxonomyNodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@NodeType", node.NodeType);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@FullPath", node.FullPath);
        command.Parameters.AddWithValue("@Level", node.Level);
        command.Parameters.AddWithValue("@IsLeaf", node.IsLeaf);
        command.Parameters.AddWithValue("@IsActive", node.IsActive);
        command.Parameters.AddWithValue("@SourceLanguage", node.SourceLanguage);
        command.Parameters.AddWithValue("@ImportedAt", node.ImportedAt);

        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<SourceTaxonomyNodeRecord?> GetNodeByIdAsync(
        long sourceTaxonomyId,
        long sourceTaxonomyNodeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyNodeId, SourceTaxonomyId, ExternalNodeId, ParentSourceTaxonomyNodeId,
                   NodeType, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomyNodes
            WHERE SourceTaxonomyId = @SourceTaxonomyId
              AND SourceTaxonomyNodeId = @SourceTaxonomyNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);
        command.Parameters.AddWithValue("@SourceTaxonomyNodeId", sourceTaxonomyNodeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadNode(reader);
    }

    public async Task<SourceTaxonomyNodeRecord?> GetNodeByExternalIdAsync(
        long sourceTaxonomyId,
        string externalNodeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyNodeId, SourceTaxonomyId, ExternalNodeId, ParentSourceTaxonomyNodeId,
                   NodeType, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomyNodes
            WHERE SourceTaxonomyId = @SourceTaxonomyId
              AND ExternalNodeId = @ExternalNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);
        command.Parameters.AddWithValue("@ExternalNodeId", externalNodeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadNode(reader);
    }

    public async Task<IReadOnlyCollection<SourceTaxonomyNodeRecord>> GetRootsAsync(
        long sourceTaxonomyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyNodeId, SourceTaxonomyId, ExternalNodeId, ParentSourceTaxonomyNodeId,
                   NodeType, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomyNodes
            WHERE SourceTaxonomyId = @SourceTaxonomyId
              AND ParentSourceTaxonomyNodeId IS NULL
            ORDER BY Level, Name, ExternalNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SourceTaxonomyNodeRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadNode(reader));
        }

        return results;
    }

    public async Task<IReadOnlyCollection<SourceTaxonomyNodeRecord>> GetChildrenAsync(
        long sourceTaxonomyId,
        long parentSourceTaxonomyNodeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyNodeId, SourceTaxonomyId, ExternalNodeId, ParentSourceTaxonomyNodeId,
                   NodeType, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage,
                   CreatedAt, UpdatedAt, ImportedAt
            FROM Catalog.SourceTaxonomyNodes
            WHERE SourceTaxonomyId = @SourceTaxonomyId
              AND ParentSourceTaxonomyNodeId = @ParentSourceTaxonomyNodeId
            ORDER BY Level, Name, ExternalNodeId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);
        command.Parameters.AddWithValue("@ParentSourceTaxonomyNodeId", parentSourceTaxonomyNodeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SourceTaxonomyNodeRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadNode(reader));
        }

        return results;
    }

    private static SourceTaxonomyDescriptorRecord ReadDescriptor(SqlDataReader reader)
    {
        return new SourceTaxonomyDescriptorRecord
        {
            SourceTaxonomyId = reader.GetInt64(0),
            Code = reader.GetString(1),
            Name = reader.GetString(2),
            ProviderCode = reader.GetString(3),
            ScopeCode = reader.IsDBNull(4) ? null : reader.GetString(4),
            ExternalTaxonomyId = reader.IsDBNull(5) ? null : reader.GetString(5),
            ExternalVersion = reader.IsDBNull(6) ? null : reader.GetString(6),
            DefaultLanguage = reader.GetString(7),
            SourceUri = reader.IsDBNull(8) ? null : reader.GetString(8),
            SourceChecksum = reader.IsDBNull(9) ? null : reader.GetString(9),
            IsActive = reader.GetBoolean(10),
            CreatedAt = reader.GetDateTime(11),
            UpdatedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            ImportedAt = reader.GetDateTime(13)
        };
    }

    private static SourceTaxonomyNodeRecord ReadNode(SqlDataReader reader)
    {
        return new SourceTaxonomyNodeRecord
        {
            SourceTaxonomyNodeId = reader.GetInt64(0),
            SourceTaxonomyId = reader.GetInt64(1),
            ExternalNodeId = reader.GetString(2),
            ParentSourceTaxonomyNodeId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
            NodeType = reader.GetString(4),
            Name = reader.GetString(5),
            FullPath = reader.GetString(6),
            Level = reader.GetInt32(7),
            IsLeaf = reader.GetBoolean(8),
            IsActive = reader.GetBoolean(9),
            SourceLanguage = reader.GetString(10),
            CreatedAt = reader.GetDateTime(11),
            UpdatedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            ImportedAt = reader.GetDateTime(13)
        };
    }
}
