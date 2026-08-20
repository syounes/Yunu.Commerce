using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;

/// <summary>
/// SQL Server adapter implementing <see cref="ICanonicalTaxonomySegmentAssociationReader"/>
/// (docs task: "Effective Segment Definitions por Canonical Taxonomy Node").
/// Uses plain ADO.NET (Microsoft.Data.SqlClient) and a single recursive CTE
/// to walk the queried node's ancestor chain (including itself) and join
/// every directly-associated SegmentDefinition, without filtering by
/// status: all filtering/precedence/deduplication is applied afterwards by
/// EffectiveSegmentDefinitionResolver in the Application layer.
/// </summary>
public sealed class SqlCanonicalTaxonomySegmentAssociationReader : ICanonicalTaxonomySegmentAssociationReader
{
    private readonly string _connectionString;

    public SqlCanonicalTaxonomySegmentAssociationReader(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<IReadOnlyCollection<CanonicalTaxonomySegmentAssociationCandidate>> GetAssociationCandidatesAsync(
        long canonicalTaxonomyNodeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH Ancestry AS
            (
                SELECT
                    CanonicalTaxonomyNodeId,
                    ParentId,
                    Depth,
                    CAST(1 AS BIT) AS IsSelf
                FROM Catalog.CanonicalTaxonomyNodes
                WHERE CanonicalTaxonomyNodeId = @CanonicalTaxonomyNodeId

                UNION ALL

                SELECT
                    Parent.CanonicalTaxonomyNodeId,
                    Parent.ParentId,
                    Parent.Depth,
                    CAST(0 AS BIT) AS IsSelf
                FROM Catalog.CanonicalTaxonomyNodes AS Parent
                INNER JOIN Ancestry
                    ON Parent.CanonicalTaxonomyNodeId = Ancestry.ParentId
            )
            SELECT
                Ancestry.CanonicalTaxonomyNodeId AS OriginCanonicalTaxonomyNodeId,
                Ancestry.Depth AS OriginNodeDepth,
                Ancestry.IsSelf,
                Association.AppliesToDescendants,
                Association.Status AS AssociationStatus,
                Association.Source AS AssociationSource,
                Association.IsRequired AS AssociationIsRequired,
                Definition.SegmentDefinitionId,
                Definition.Code,
                Definition.Name,
                Definition.Status AS DefinitionStatus,
                Definition.AssignmentScope
            FROM Ancestry
            INNER JOIN Catalog.CanonicalTaxonomyNodeSegmentDefinitions AS Association
                ON Association.CanonicalTaxonomyNodeId = Ancestry.CanonicalTaxonomyNodeId
            INNER JOIN Catalog.SegmentDefinitions AS Definition
                ON Definition.SegmentDefinitionId = Association.SegmentDefinitionId
            OPTION (MAXRECURSION 100);
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@CanonicalTaxonomyNodeId", canonicalTaxonomyNodeId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<CanonicalTaxonomySegmentAssociationCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CanonicalTaxonomySegmentAssociationCandidate
            {
                OriginCanonicalTaxonomyNodeId = reader.GetInt64(0),
                OriginNodeDepth = reader.GetInt16(1),
                IsSelf = reader.GetBoolean(2),
                AppliesToDescendants = reader.GetBoolean(3),
                AssociationStatus = reader.GetString(4),
                AssociationSource = reader.GetString(5),
                AssociationIsRequired = reader.GetBoolean(6),
                SegmentDefinitionId = reader.GetInt64(7),
                Code = reader.GetString(8),
                Name = reader.GetString(9),
                DefinitionStatus = reader.GetString(10),
                AssignmentScope = reader.GetString(11)
            });
        }

        return results;
    }
}
