using Microsoft.Data.SqlClient;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ISourceTaxonomyImportStore"/>
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §8, §11, §12).
/// Persists to Integration.SourceTaxonomyImports
/// (deploy/databases/sqlserver/014-create-source-taxonomy-foundation.sql).
///
/// <see cref="StartAsync"/> and <see cref="MarkFailedAsync"/> each commit
/// independently (their own connection, no shared transaction) so the
/// Started row survives even if the later synchronization transaction is
/// rolled back. Completion is handled separately by
/// <see cref="SqlSourceTaxonomySynchronizationStore"/> from inside the same
/// transaction that applies the header/node changes.
/// </summary>
public sealed class SqlSourceTaxonomyImportStore : ISourceTaxonomyImportStore
{
    private readonly string _connectionString;

    public SqlSourceTaxonomyImportStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<long> StartAsync(
        long sourceTaxonomyId,
        string adapterCode,
        string? sourceUri,
        string? externalVersion,
        string? sourceChecksum,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Integration.SourceTaxonomyImports
                (SourceTaxonomyId, AdapterCode, SourceUri, ExternalVersion, SourceChecksum,
                 StartedAt, NodeCount, InsertedCount, UpdatedCount, DeactivatedCount, Status)
            OUTPUT INSERTED.ImportId
            VALUES
                (@SourceTaxonomyId, @AdapterCode, @SourceUri, @ExternalVersion, @SourceChecksum,
                 @StartedAt, 0, 0, 0, 0, 'Started')
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);
        command.Parameters.AddWithValue("@AdapterCode", adapterCode);
        command.Parameters.AddWithValue("@SourceUri", (object?)sourceUri ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExternalVersion", (object?)externalVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("@SourceChecksum", (object?)sourceChecksum ?? DBNull.Value);
        command.Parameters.AddWithValue("@StartedAt", startedAtUtc);

        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task MarkFailedAsync(
        long importId,
        string errorMessage,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Integration.SourceTaxonomyImports
            SET CompletedAt = @CompletedAt,
                Status = 'Failed',
                ErrorMessage = @ErrorMessage
            WHERE ImportId = @ImportId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ImportId", importId);
        command.Parameters.AddWithValue("@CompletedAt", completedAtUtc);
        command.Parameters.AddWithValue("@ErrorMessage", Truncate(errorMessage));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Truncate(string message)
    {
        const int maxLength = 2000;
        return message.Length > maxLength ? message[..maxLength] : message;
    }
}
