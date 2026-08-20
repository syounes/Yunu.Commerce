using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.SegmentDefinitions;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ISegmentDefinitionUsageReader"/>
/// (docs task: "Yunu.Commerce V8 - Lifecycle + Usage Guards de Segments").
/// Uses plain ADO.NET (Microsoft.Data.SqlClient), matching
/// <see cref="SqlSegmentDefinitionRepository"/>.
/// </summary>
public sealed class SqlSegmentDefinitionUsageReader : ISegmentDefinitionUsageReader
{
    private readonly string _connectionString;

    public SqlSegmentDefinitionUsageReader(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<bool> HasApprovedCanonicalTaxonomyAssociationAsync(
        SegmentDefinitionId segmentDefinitionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM Catalog.CanonicalTaxonomyNodeSegmentDefinitions
            WHERE SegmentDefinitionId = @SegmentDefinitionId
              AND Status = N'Approved'
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@SegmentDefinitionId", segmentDefinitionId.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }
}
