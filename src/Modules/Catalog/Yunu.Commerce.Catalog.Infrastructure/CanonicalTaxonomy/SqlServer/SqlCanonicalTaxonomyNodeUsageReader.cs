using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ICanonicalTaxonomyNodeUsageReader"/>
/// (docs task: "Yunu.Commerce V9 - Canonical Taxonomy Lifecycle + Usage
/// Guards"). Uses plain ADO.NET (Microsoft.Data.SqlClient), matching
/// <see cref="SqlCanonicalTaxonomyRepository"/>.
/// </summary>
public sealed class SqlCanonicalTaxonomyNodeUsageReader : ICanonicalTaxonomyNodeUsageReader
{
    private readonly string _connectionString;

    public SqlCanonicalTaxonomyNodeUsageReader(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<bool> HasApprovedSegmentAssociationAsync(
        CanonicalTaxonomyNodeId canonicalTaxonomyNodeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM Catalog.CanonicalTaxonomyNodeSegmentDefinitions
            WHERE CanonicalTaxonomyNodeId = @CanonicalTaxonomyNodeId
              AND Status = N'Approved'
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CanonicalTaxonomyNodeId", canonicalTaxonomyNodeId.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }
}
