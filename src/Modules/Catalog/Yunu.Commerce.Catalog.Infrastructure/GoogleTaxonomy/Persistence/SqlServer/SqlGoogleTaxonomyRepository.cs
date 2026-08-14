using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="IGoogleTaxonomyRepository"/>
/// (docs/adr/0003-database-per-bounded-context.md §9). Uses plain ADO.NET
/// (Microsoft.Data.SqlClient) rather than an ORM: the synchronization logic is a
/// small set of upsert/deactivate statements, and introducing EF Core or Dapper
/// here would add unjustified complexity (docs §38, "avoid unnecessary abstractions").
///
/// Persists to GoogleTaxonomyCategories and GoogleTaxonomyImports
/// (deploy/sql/001-google-taxonomy-tables.sql). Synchronization never issues a
/// bulk DELETE; existing rows are upserted and rows absent from the current feed
/// are marked IsActive = 0 (docs task: "Synchronization behavior").
/// </summary>
public sealed class SqlGoogleTaxonomyRepository : IGoogleTaxonomyRepository
{
    private readonly string _connectionString;

    public SqlGoogleTaxonomyRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<GoogleTaxonomyPersistenceResult> SynchronizeAsync(
        IReadOnlyCollection<GoogleTaxonomyCategoryItem> categories,
        string sourceLanguage,
        string sourceUrl,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var importId = Guid.NewGuid();

        try
        {
            await InsertImportStartedAsync(connection, transaction, importId, sourceLanguage, sourceUrl, importedAtUtc, cancellationToken);

            var existingCategories = await LoadExistingCategoriesAsync(connection, transaction, cancellationToken);

            var inserted = 0;
            var updated = 0;

            var currentIds = new HashSet<int>();

            foreach (var category in categories)
            {
                currentIds.Add(category.GoogleCategoryId);

                if (existingCategories.TryGetValue(category.GoogleCategoryId, out var existing))
                {
                    var hasChanges =
                        existing.ParentGoogleCategoryId != category.ParentGoogleCategoryId ||
                        existing.Name != category.Name ||
                        existing.FullPath != category.FullPath ||
                        existing.Level != category.Level ||
                        existing.IsLeaf != category.IsLeaf ||
                        existing.SourceLanguage != sourceLanguage ||
                        !existing.IsActive;

                    if (hasChanges)
                    {
                        await UpdateCategoryAsync(connection, transaction, category, sourceLanguage, importedAtUtc, cancellationToken);
                        updated++;
                    }
                    else
                    {
                        await TouchImportedAtAsync(connection, transaction, category.GoogleCategoryId, importedAtUtc, cancellationToken);
                    }
                }
                else
                {
                    await InsertCategoryAsync(connection, transaction, category, sourceLanguage, importedAtUtc, cancellationToken);
                    inserted++;
                }
            }

            var deactivated = await DeactivateMissingCategoriesAsync(connection, transaction, existingCategories, currentIds, importedAtUtc, cancellationToken);

            await CompleteImportAsync(connection, transaction, importId, categories.Count, inserted, updated, deactivated, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new GoogleTaxonomyPersistenceResult(categories.Count, inserted, updated, deactivated);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            await using var failureConnection = new SqlConnection(_connectionString);
            await failureConnection.OpenAsync(cancellationToken);

            await MarkImportFailedAsync(failureConnection, importId, SanitizeErrorMessage(ex), cancellationToken);

            throw;
        }
    }

    public async Task<GoogleTaxonomyCategoryResponse?> GetByIdAsync(int googleCategoryId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive
            FROM GoogleTaxonomyCategories
            WHERE GoogleCategoryId = @GoogleCategoryId
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@GoogleCategoryId", googleCategoryId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadCategoryResponse(reader);
    }

    public async Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 100);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (@Limit) GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive
            FROM GoogleTaxonomyCategories
            WHERE IsActive = 1
              AND (Name LIKE @Pattern OR FullPath LIKE @Pattern)
            ORDER BY Level, Name
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Limit", effectiveLimit);
        command.Parameters.AddWithValue("@Pattern", $"%{query}%");

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleTaxonomyCategoryResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadCategoryResponse(reader));
        }

        return results;
    }

    public async Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> GetAncestorsAsync(
        int googleCategoryId,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var chain = new List<GoogleTaxonomyCategoryResponse>();
        int? currentId = googleCategoryId;
        var visited = new HashSet<int>();

        while (currentId.HasValue && visited.Add(currentId.Value))
        {
            const string sql = """
                SELECT GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive
                FROM GoogleTaxonomyCategories
                WHERE GoogleCategoryId = @GoogleCategoryId
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@GoogleCategoryId", currentId.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                break;
            }

            var category = ReadCategoryResponse(reader);
            chain.Add(category);
            currentId = category.ParentGoogleCategoryId;
        }

        chain.Reverse();

        return chain
            .Where(category => category.GoogleCategoryId != googleCategoryId)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive
            FROM GoogleTaxonomyCategories
            WHERE IsActive = 1
            ORDER BY GoogleCategoryId
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleTaxonomyCategoryResponse>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadCategoryResponse(reader));
        }

        return results;
    }

    private static GoogleTaxonomyCategoryResponse ReadCategoryResponse(SqlDataReader reader)
    {
        return new GoogleTaxonomyCategoryResponse
        {
            GoogleCategoryId = reader.GetInt32(0),
            ParentGoogleCategoryId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            Name = reader.GetString(2),
            FullPath = reader.GetString(3),
            Level = reader.GetInt32(4),
            IsLeaf = reader.GetBoolean(5),
            IsActive = reader.GetBoolean(6)
        };
    }

    private static async Task<Dictionary<int, ExistingCategoryRow>> LoadExistingCategoriesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage
            FROM GoogleTaxonomyCategories
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new Dictionary<int, ExistingCategoryRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var googleCategoryId = reader.GetInt32(0);

            result[googleCategoryId] = new ExistingCategoryRow(
                ParentGoogleCategoryId: reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Name: reader.GetString(2),
                FullPath: reader.GetString(3),
                Level: reader.GetInt32(4),
                IsLeaf: reader.GetBoolean(5),
                IsActive: reader.GetBoolean(6),
                SourceLanguage: reader.GetString(7));
        }

        return result;
    }

    private static async Task InsertCategoryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        GoogleTaxonomyCategoryItem category,
        string sourceLanguage,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO GoogleTaxonomyCategories
                (GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage, CreatedAt, ImportedAt)
            VALUES
                (@GoogleCategoryId, @ParentGoogleCategoryId, @Name, @FullPath, @Level, @IsLeaf, 1, @SourceLanguage, @Now, @ImportedAt)
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@GoogleCategoryId", category.GoogleCategoryId);
        command.Parameters.AddWithValue("@ParentGoogleCategoryId", (object?)category.ParentGoogleCategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", category.Name);
        command.Parameters.AddWithValue("@FullPath", category.FullPath);
        command.Parameters.AddWithValue("@Level", category.Level);
        command.Parameters.AddWithValue("@IsLeaf", category.IsLeaf);
        command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateCategoryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        GoogleTaxonomyCategoryItem category,
        string sourceLanguage,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE GoogleTaxonomyCategories
            SET ParentGoogleCategoryId = @ParentGoogleCategoryId,
                Name = @Name,
                FullPath = @FullPath,
                Level = @Level,
                IsLeaf = @IsLeaf,
                IsActive = 1,
                SourceLanguage = @SourceLanguage,
                UpdatedAt = @Now,
                ImportedAt = @ImportedAt
            WHERE GoogleCategoryId = @GoogleCategoryId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@GoogleCategoryId", category.GoogleCategoryId);
        command.Parameters.AddWithValue("@ParentGoogleCategoryId", (object?)category.ParentGoogleCategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Name", category.Name);
        command.Parameters.AddWithValue("@FullPath", category.FullPath);
        command.Parameters.AddWithValue("@Level", category.Level);
        command.Parameters.AddWithValue("@IsLeaf", category.IsLeaf);
        command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TouchImportedAtAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int googleCategoryId,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE GoogleTaxonomyCategories
            SET ImportedAt = @ImportedAt
            WHERE GoogleCategoryId = @GoogleCategoryId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@GoogleCategoryId", googleCategoryId);
        command.Parameters.AddWithValue("@ImportedAt", importedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> DeactivateMissingCategoriesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Dictionary<int, ExistingCategoryRow> existingCategories,
        HashSet<int> currentIds,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        var toDeactivate = existingCategories
            .Where(kvp => kvp.Value.IsActive && !currentIds.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToArray();

        foreach (var googleCategoryId in toDeactivate)
        {
            const string sql = """
                UPDATE GoogleTaxonomyCategories
                SET IsActive = 0,
                    UpdatedAt = @Now
                WHERE GoogleCategoryId = @GoogleCategoryId
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@GoogleCategoryId", googleCategoryId);
            command.Parameters.AddWithValue("@Now", DateTime.UtcNow);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return toDeactivate.Length;
    }

    private static async Task InsertImportStartedAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid importId,
        string sourceLanguage,
        string sourceUrl,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO GoogleTaxonomyImports
                (ImportId, SourceLanguage, SourceUrl, StartedAt, CategoryCount, InsertedCount, UpdatedCount, DeactivatedCount, Status)
            VALUES
                (@ImportId, @SourceLanguage, @SourceUrl, @StartedAt, 0, 0, 0, 0, 'Processing')
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ImportId", importId);
        command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
        command.Parameters.AddWithValue("@SourceUrl", sourceUrl);
        command.Parameters.AddWithValue("@StartedAt", startedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CompleteImportAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid importId,
        int categoryCount,
        int inserted,
        int updated,
        int deactivated,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE GoogleTaxonomyImports
            SET CompletedAt = @CompletedAt,
                CategoryCount = @CategoryCount,
                InsertedCount = @InsertedCount,
                UpdatedCount = @UpdatedCount,
                DeactivatedCount = @DeactivatedCount,
                Status = 'Completed'
            WHERE ImportId = @ImportId
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@ImportId", importId);
        command.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@CategoryCount", categoryCount);
        command.Parameters.AddWithValue("@InsertedCount", inserted);
        command.Parameters.AddWithValue("@UpdatedCount", updated);
        command.Parameters.AddWithValue("@DeactivatedCount", deactivated);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkImportFailedAsync(
        SqlConnection connection,
        Guid importId,
        string sanitizedErrorMessage,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE GoogleTaxonomyImports
            SET CompletedAt = @CompletedAt,
                Status = 'Failed',
                ErrorMessage = @ErrorMessage
            WHERE ImportId = @ImportId
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ImportId", importId);
        command.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);
        command.Parameters.AddWithValue("@ErrorMessage", sanitizedErrorMessage);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SanitizeErrorMessage(Exception ex)
    {
        var message = ex.Message;

        const int maxLength = 2000;

        return message.Length > maxLength ? message[..maxLength] : message;
    }

    private sealed record ExistingCategoryRow(
        int? ParentGoogleCategoryId,
        string Name,
        string FullPath,
        int Level,
        bool IsLeaf,
        bool IsActive,
        string SourceLanguage);
}
