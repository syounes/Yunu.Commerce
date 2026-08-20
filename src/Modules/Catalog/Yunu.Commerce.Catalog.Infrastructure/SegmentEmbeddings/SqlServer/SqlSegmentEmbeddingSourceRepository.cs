using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentEmbeddings.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ISegmentEmbeddingSourceRepository"/>
/// (docs task: "Implementar sincronização de embeddings de segmentos"). Uses
/// plain ADO.NET (Microsoft.Data.SqlClient), matching the convention already
/// established by <see cref="Catalog.Infrastructure.AttributeEmbeddings.SqlServer.SqlAttributeEmbeddingSourceRepository"/>:
/// no ORM, parameterized queries.
///
/// Reuses the existing Catalog SQL Server connection
/// (<see cref="GoogleTaxonomySqlOptions"/>, section "Catalog:GoogleTaxonomySql")
/// since Catalog.SegmentDefinitions / Catalog.SegmentOptions live in the same
/// database; no second SQL Server configuration section is introduced.
///
/// Only Segment Definitions with Status = 'Active' are read, and only Segment
/// Options with Status = 'Active' whose owning Segment Definition is also
/// active. AssignmentScope is copied from the parent Definition to each
/// Option. UpdatedAt values read from SQL Server represent UTC and are
/// normalized with <see cref="DateTime.SpecifyKind"/> before being handed to
/// the PostgreSQL adapter (timestamptz columns).
/// </summary>
public sealed class SqlSegmentEmbeddingSourceRepository : ISegmentEmbeddingSourceRepository
{
    private readonly string _connectionString;

    public SqlSegmentEmbeddingSourceRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IReadOnlyCollection<SegmentDefinitionSource>> GetActiveDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SegmentDefinitionId, Code, Name, Description, SemanticText,
                   SelectionMode, AssignmentScope, UpdatedAt
            FROM Catalog.SegmentDefinitions
            WHERE Status = 'Active'
            ORDER BY Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SegmentDefinitionSource>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SegmentDefinitionSource
            {
                SegmentDefinitionId = reader.GetInt64(0),
                Code = reader.GetString(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                SemanticText = reader.IsDBNull(4) ? null : reader.GetString(4),
                SelectionMode = reader.GetString(5),
                AssignmentScope = reader.GetString(6),
                UpdatedAt = DateTime.SpecifyKind(reader.GetDateTime(7), DateTimeKind.Utc)
            });
        }

        return results;
    }

    public async Task<IReadOnlyCollection<SegmentOptionSource>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT o.SegmentOptionId, o.SegmentDefinitionId, d.Code, d.Name, o.Code, o.Name,
                   o.Description, o.SemanticText, d.AssignmentScope, o.DisplayOrder, o.UpdatedAt
            FROM Catalog.SegmentOptions o
            INNER JOIN Catalog.SegmentDefinitions d ON d.SegmentDefinitionId = o.SegmentDefinitionId
            WHERE o.Status = 'Active'
              AND d.Status = 'Active'
            ORDER BY d.Code, o.Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SegmentOptionSource>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SegmentOptionSource
            {
                SegmentOptionId = reader.GetInt64(0),
                SegmentDefinitionId = reader.GetInt64(1),
                SegmentCode = reader.GetString(2),
                SegmentName = reader.GetString(3),
                OptionCode = reader.GetString(4),
                OptionName = reader.GetString(5),
                OptionDescription = reader.IsDBNull(6) ? null : reader.GetString(6),
                OptionSemanticText = reader.IsDBNull(7) ? null : reader.GetString(7),
                AssignmentScope = reader.GetString(8),
                DisplayOrder = reader.GetInt32(9),
                UpdatedAt = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc)
            });
        }

        return results;
    }
}
