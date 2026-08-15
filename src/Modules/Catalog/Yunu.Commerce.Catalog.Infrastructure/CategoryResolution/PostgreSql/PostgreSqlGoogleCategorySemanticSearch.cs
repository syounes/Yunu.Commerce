using Npgsql;
using Pgvector;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Catalog.Infrastructure.CategoryResolution.PostgreSql;

/// <summary>
/// PostgreSQL + pgvector adapter implementing <see
/// cref="IGoogleCategorySemanticSearch"/> (docs task: "Google Category
/// Resolution"). Reads from public.google_taxonomy_embeddings using cosine
/// distance (1 - (embedding &lt;=&gt; @query)), mirroring the convention
/// already established for attribute embeddings (<see
/// cref="Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.PostgreSql.PostgreSqlAttributeSemanticSearch"/>).
///
/// Unlike public.sku_attribute_embeddings, the current
/// google_taxonomy_embeddings schema
/// (deploy/docker/postgres/init/002-create-google-taxonomy-embeddings.sql)
/// does not have is_active, content_hash or embedded_content_hash columns:
/// every persisted row is a fresh upsert (see
/// PostgreSqlGoogleTaxonomyEmbeddingRepository.UpsertAsync), so those filters
/// are intentionally omitted here rather than assuming columns that do not
/// exist. This is a documented schema difference, not an oversight; the
/// schema itself is not altered automatically per docs task constraints.
/// The <paramref name="locale"/> parameter is accepted for interface
/// symmetry with attribute semantic search, but Google Taxonomy embeddings
/// are not currently segmented by locale.
/// </summary>
public sealed class PostgreSqlGoogleCategorySemanticSearch : IGoogleCategorySemanticSearch
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlGoogleCategorySemanticSearch(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<GoogleCategorySemanticCandidate>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        string locale,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT google_category_id, category_path, 1 - (embedding <=> @query) AS similarity
            FROM public.google_taxonomy_embeddings
            WHERE embedding IS NOT NULL
            ORDER BY embedding <=> @query
            LIMIT @topK;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("query", new Vector(queryEmbedding));
        command.Parameters.AddWithValue("topK", topK);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleCategorySemanticCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GoogleCategorySemanticCandidate(
                GoogleCategoryId: reader.GetInt32(0),
                CategoryPath: reader.GetString(1),
                Similarity: reader.GetDouble(2)));
        }

        return results;
    }
}
