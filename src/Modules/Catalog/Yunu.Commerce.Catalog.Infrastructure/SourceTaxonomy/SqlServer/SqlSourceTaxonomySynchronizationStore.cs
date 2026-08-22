using Microsoft.Data.SqlClient;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ISourceTaxonomySynchronizationStore"/>
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §13-§17). Applies one
/// validated <see cref="SourceTaxonomySnapshot"/> atomically inside a single
/// transaction using plain ADO.NET (Microsoft.Data.SqlClient); no EF Core,
/// Dapper or SQL MERGE.
///
/// Design:
/// 1. Locks the target Catalog.SourceTaxonomies row (UPDLOCK, ROWLOCK) so the
///    checksum-skip decision (§16) and header update are serialized safely
///    against concurrent synchronization of the SAME SourceTaxonomy.
/// 2. If the locked row's SourceChecksum equals the snapshot checksum (both
///    non-blank), skips node synchronization entirely and only refreshes
///    header metadata + import history.
/// 3. Otherwise loads existing nodes for this SourceTaxonomyId in one query
///    (avoids N+1 lookups), then performs the ADR-0014 two-pass hierarchy
///    resolution: pass 1 upserts scalar node state and builds the
///    ExternalNodeId -> SourceTaxonomyNodeId map; pass 2 resolves
///    ParentExternalNodeId into ParentSourceTaxonomyNodeId. Nodes present in
///    the persisted set but absent from the snapshot are deactivated, never
///    hard deleted.
/// 4. Marks the pre-existing (Started) import row Completed from inside this
///    same transaction, so a Completed row can never survive a rollback.
/// </summary>
public sealed class SqlSourceTaxonomySynchronizationStore : ISourceTaxonomySynchronizationStore
{
    private readonly string _connectionString;

    public SqlSourceTaxonomySynchronizationStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<SourceTaxonomySynchronizationResult> ApplyAsync(
        long sourceTaxonomyId,
        long importId,
        SourceTaxonomySnapshot snapshot,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var currentSource = await LoadAndLockSourceAsync(connection, transaction, sourceTaxonomyId, cancellationToken);

            var canSkipByChecksum =
                !string.IsNullOrWhiteSpace(currentSource.SourceChecksum)
                && !string.IsNullOrWhiteSpace(snapshot.Descriptor.SourceChecksum)
                && string.Equals(currentSource.SourceChecksum, snapshot.Descriptor.SourceChecksum, StringComparison.Ordinal);

            SourceTaxonomySynchronizationResult result;

            if (canSkipByChecksum)
            {
                await RefreshHeaderAsync(connection, transaction, sourceTaxonomyId, currentSource, snapshot.Descriptor, importedAtUtc, cancellationToken);

                result = new SourceTaxonomySynchronizationResult
                {
                    NodeCount = snapshot.Nodes.Count,
                    InsertedCount = 0,
                    UpdatedCount = 0,
                    DeactivatedCount = 0,
                    WasSkippedByChecksum = true
                };
            }
            else
            {
                var (inserted, updated, deactivated) = await SynchronizeNodesAsync(
                    connection,
                    transaction,
                    sourceTaxonomyId,
                    snapshot,
                    importedAtUtc,
                    cancellationToken);

                await RefreshHeaderAsync(connection, transaction, sourceTaxonomyId, currentSource, snapshot.Descriptor, importedAtUtc, cancellationToken);

                result = new SourceTaxonomySynchronizationResult
                {
                    NodeCount = snapshot.Nodes.Count,
                    InsertedCount = inserted,
                    UpdatedCount = updated,
                    DeactivatedCount = deactivated,
                    WasSkippedByChecksum = false
                };
            }

            await CompleteImportAsync(connection, transaction, importId, result, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<CurrentSourceRow> LoadAndLockSourceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ScopeCode, ExternalTaxonomyId, SourceChecksum
            FROM Catalog.SourceTaxonomies WITH (UPDLOCK, ROWLOCK)
            WHERE SourceTaxonomyId = @SourceTaxonomyId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new SourceTaxonomyNotFoundException(sourceTaxonomyId);
        }

        return new CurrentSourceRow(
            ScopeCode: reader.IsDBNull(0) ? null : reader.GetString(0),
            ExternalTaxonomyId: reader.IsDBNull(1) ? null : reader.GetString(1),
            SourceChecksum: reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static async Task<(int Inserted, int Updated, int Deactivated)> SynchronizeNodesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyId,
        SourceTaxonomySnapshot snapshot,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        var existingNodes = await LoadExistingNodesAsync(connection, transaction, sourceTaxonomyId, cancellationToken);

        var idByExternalId = new Dictionary<string, long>(StringComparer.Ordinal);
        var updatedExternalIds = new HashSet<string>(StringComparer.Ordinal);
        var seenExternalIds = new HashSet<string>(StringComparer.Ordinal);
        var inserted = 0;

        // Pass 1: upsert scalar node state, resolve ExternalNodeId -> SourceTaxonomyNodeId.
        foreach (var node in snapshot.Nodes)
        {
            seenExternalIds.Add(node.ExternalNodeId);

            if (existingNodes.TryGetValue(node.ExternalNodeId, out var existing))
            {
                idByExternalId[node.ExternalNodeId] = existing.SourceTaxonomyNodeId;

                var scalarChanged =
                    existing.NodeType != node.NodeType ||
                    existing.Name != node.Name ||
                    existing.FullPath != node.FullPath ||
                    existing.Level != node.Level ||
                    existing.IsLeaf != node.IsLeaf ||
                    existing.IsActive != node.IsActive ||
                    existing.SourceLanguage != snapshot.Descriptor.Locale;

                if (scalarChanged)
                {
                    await UpdateNodeScalarAsync(connection, transaction, existing.SourceTaxonomyNodeId, node, snapshot.Descriptor.Locale, importedAtUtc, cancellationToken);
                    updatedExternalIds.Add(node.ExternalNodeId);
                }
                else
                {
                    await TouchNodeImportedAtAsync(connection, transaction, existing.SourceTaxonomyNodeId, importedAtUtc, cancellationToken);
                }
            }
            else
            {
                var newId = await InsertNodeAsync(connection, transaction, sourceTaxonomyId, node, snapshot.Descriptor.Locale, importedAtUtc, cancellationToken);
                idByExternalId[node.ExternalNodeId] = newId;
                inserted++;
            }
        }

        // Pass 2: resolve ParentExternalNodeId -> ParentSourceTaxonomyNodeId.
        foreach (var node in snapshot.Nodes)
        {
            var nodeId = idByExternalId[node.ExternalNodeId];
            long? resolvedParentId = node.ParentExternalNodeId is null ? null : idByExternalId[node.ParentExternalNodeId];

            var isNewNode = !existingNodes.TryGetValue(node.ExternalNodeId, out var existing);
            var currentParentId = isNewNode ? null : existing!.ParentSourceTaxonomyNodeId;

            if (currentParentId == resolvedParentId)
            {
                continue;
            }

            await UpdateNodeParentAsync(connection, transaction, nodeId, resolvedParentId, importedAtUtc, cancellationToken);

            if (!isNewNode)
            {
                updatedExternalIds.Add(node.ExternalNodeId);
            }
        }

        // Deactivate persisted nodes absent from the complete snapshot. Never hard delete.
        var deactivated = 0;

        foreach (var (externalNodeId, existing) in existingNodes)
        {
            if (seenExternalIds.Contains(externalNodeId) || !existing.IsActive)
            {
                continue;
            }

            await DeactivateNodeAsync(connection, transaction, existing.SourceTaxonomyNodeId, importedAtUtc, cancellationToken);
            deactivated++;
        }

        return (inserted, updatedExternalIds.Count, deactivated);
    }

    private static async Task<Dictionary<string, ExistingNodeRow>> LoadExistingNodesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SourceTaxonomyNodeId, ExternalNodeId, ParentSourceTaxonomyNodeId, NodeType, Name,
                   FullPath, Level, IsLeaf, IsActive, SourceLanguage
            FROM Catalog.SourceTaxonomyNodes
            WHERE SourceTaxonomyId = @SourceTaxonomyId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new Dictionary<string, ExistingNodeRow>(StringComparer.Ordinal);

        while (await reader.ReadAsync(cancellationToken))
        {
            var externalNodeId = reader.GetString(1);

            result[externalNodeId] = new ExistingNodeRow(
                SourceTaxonomyNodeId: reader.GetInt64(0),
                ParentSourceTaxonomyNodeId: reader.IsDBNull(2) ? null : reader.GetInt64(2),
                NodeType: reader.GetString(3),
                Name: reader.GetString(4),
                FullPath: reader.GetString(5),
                Level: reader.GetInt32(6),
                IsLeaf: reader.GetBoolean(7),
                IsActive: reader.GetBoolean(8),
                SourceLanguage: reader.GetString(9));
        }

        return result;
    }

    private static async Task<long> InsertNodeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyId,
        SourceTaxonomySnapshotNode node,
        string locale,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Catalog.SourceTaxonomyNodes
                (SourceTaxonomyId, ExternalNodeId, ParentSourceTaxonomyNodeId, NodeType, Name,
                 FullPath, Level, IsLeaf, IsActive, SourceLanguage, ImportedAt)
            OUTPUT INSERTED.SourceTaxonomyNodeId
            VALUES
                (@SourceTaxonomyId, @ExternalNodeId, NULL, @NodeType, @Name,
                 @FullPath, @Level, @IsLeaf, @IsActive, @SourceLanguage, @ImportedAt)
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);
        command.Parameters.AddWithValue("@ExternalNodeId", node.ExternalNodeId);
        command.Parameters.AddWithValue("@NodeType", node.NodeType);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@FullPath", node.FullPath);
        command.Parameters.AddWithValue("@Level", node.Level);
        command.Parameters.AddWithValue("@IsLeaf", node.IsLeaf);
        command.Parameters.AddWithValue("@IsActive", node.IsActive);
        command.Parameters.AddWithValue("@SourceLanguage", locale);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task UpdateNodeScalarAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyNodeId,
        SourceTaxonomySnapshotNode node,
        string locale,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Catalog.SourceTaxonomyNodes
            SET NodeType = @NodeType,
                Name = @Name,
                FullPath = @FullPath,
                Level = @Level,
                IsLeaf = @IsLeaf,
                IsActive = @IsActive,
                SourceLanguage = @SourceLanguage,
                UpdatedAt = @Now,
                ImportedAt = @ImportedAt
            WHERE SourceTaxonomyNodeId = @SourceTaxonomyNodeId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyNodeId", sourceTaxonomyNodeId);
        command.Parameters.AddWithValue("@NodeType", node.NodeType);
        command.Parameters.AddWithValue("@Name", node.Name);
        command.Parameters.AddWithValue("@FullPath", node.FullPath);
        command.Parameters.AddWithValue("@Level", node.Level);
        command.Parameters.AddWithValue("@IsLeaf", node.IsLeaf);
        command.Parameters.AddWithValue("@IsActive", node.IsActive);
        command.Parameters.AddWithValue("@SourceLanguage", locale);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateNodeParentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyNodeId,
        long? parentSourceTaxonomyNodeId,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Catalog.SourceTaxonomyNodes
            SET ParentSourceTaxonomyNodeId = @ParentSourceTaxonomyNodeId,
                UpdatedAt = @Now,
                ImportedAt = @ImportedAt
            WHERE SourceTaxonomyNodeId = @SourceTaxonomyNodeId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyNodeId", sourceTaxonomyNodeId);
        command.Parameters.AddWithValue("@ParentSourceTaxonomyNodeId", (object?)parentSourceTaxonomyNodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TouchNodeImportedAtAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyNodeId,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Catalog.SourceTaxonomyNodes
            SET ImportedAt = @ImportedAt
            WHERE SourceTaxonomyNodeId = @SourceTaxonomyNodeId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyNodeId", sourceTaxonomyNodeId);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeactivateNodeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyNodeId,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Catalog.SourceTaxonomyNodes
            SET IsActive = 0,
                UpdatedAt = @Now
            WHERE SourceTaxonomyNodeId = @SourceTaxonomyNodeId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyNodeId", sourceTaxonomyNodeId);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RefreshHeaderAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long sourceTaxonomyId,
        CurrentSourceRow currentSource,
        SourceTaxonomySnapshotDescriptor snapshotDescriptor,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        // ScopeCode/ExternalTaxonomyId enrichment rule: only fill from snapshot
        // when the existing value is null. Conflicting non-null values were
        // already rejected before reaching this store.
        var scopeCode = currentSource.ScopeCode ?? snapshotDescriptor.ScopeCode;
        var externalTaxonomyId = currentSource.ExternalTaxonomyId ?? snapshotDescriptor.ExternalTaxonomyId;

        const string sql = """
            UPDATE Catalog.SourceTaxonomies
            SET ScopeCode = @ScopeCode,
                ExternalTaxonomyId = @ExternalTaxonomyId,
                ExternalVersion = @ExternalVersion,
                DefaultLanguage = @DefaultLanguage,
                SourceUri = @SourceUri,
                SourceChecksum = @SourceChecksum,
                ImportedAt = @ImportedAt,
                UpdatedAt = @Now
            WHERE SourceTaxonomyId = @SourceTaxonomyId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@SourceTaxonomyId", sourceTaxonomyId);
        command.Parameters.AddWithValue("@ScopeCode", (object?)scopeCode ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExternalTaxonomyId", (object?)externalTaxonomyId ?? DBNull.Value);
        command.Parameters.AddWithValue("@ExternalVersion", (object?)snapshotDescriptor.ExternalVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("@DefaultLanguage", snapshotDescriptor.Locale);
        command.Parameters.AddWithValue("@SourceUri", (object?)snapshotDescriptor.SourceUri ?? DBNull.Value);
        command.Parameters.AddWithValue("@SourceChecksum", (object?)snapshotDescriptor.SourceChecksum ?? DBNull.Value);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteImportAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long importId,
        SourceTaxonomySynchronizationResult result,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Integration.SourceTaxonomyImports
            SET CompletedAt = @CompletedAt,
                NodeCount = @NodeCount,
                InsertedCount = @InsertedCount,
                UpdatedCount = @UpdatedCount,
                DeactivatedCount = @DeactivatedCount,
                Status = 'Completed',
                ErrorMessage = NULL
            WHERE ImportId = @ImportId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ImportId", importId);
        command.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@NodeCount", result.NodeCount);
        command.Parameters.AddWithValue("@InsertedCount", result.InsertedCount);
        command.Parameters.AddWithValue("@UpdatedCount", result.UpdatedCount);
        command.Parameters.AddWithValue("@DeactivatedCount", result.DeactivatedCount);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record CurrentSourceRow(string? ScopeCode, string? ExternalTaxonomyId, string? SourceChecksum);

    private sealed record ExistingNodeRow(
        long SourceTaxonomyNodeId,
        long? ParentSourceTaxonomyNodeId,
        string NodeType,
        string Name,
        string FullPath,
        int Level,
        bool IsLeaf,
        bool IsActive,
        string SourceLanguage);
}
