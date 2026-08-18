using Npgsql;
using Pgvector;
using Yunu.Commerce.Catalog.Application.AttributeResolution;

namespace Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.PostgreSql;

/// <summary>
/// PostgreSQL + pgvector adapter implementing <see cref="IAttributeSemanticSearch"/>
/// (docs task: "Semantic attribute hint resolution"). Reads from
/// public.sku_attribute_embeddings using cosine distance (1 - (embedding
/// &lt;=&gt; @query)), matching the convention already established for the
/// attribute embedding synchronization pipeline
/// (deploy/databases/postgres/003_create_sku_attribute_vectors.sql).
///
/// Only rows with is_active = true, embedding IS NOT NULL and
/// embedded_content_hash = content_hash are considered, so pending/stale
/// embeddings are never returned as candidates. entity_id is the attribute
/// code (definitions) or "{attributeCode}:{optionCode}" (options), never a
/// numeric SQL Server id; callers must still hydrate/validate results against
/// SQL Server.
/// </summary>
public sealed class PostgreSqlAttributeSemanticSearch : IAttributeSemanticSearch
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlAttributeSemanticSearch(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<SemanticAttributeCandidate>> SearchDefinitionsAsync(
        ReadOnlyMemory<float> embedding,
        int topK,
        string locale,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT attribute_code, name, 1 - (embedding <=> @query) AS similarity
            FROM public.sku_attribute_embeddings
            WHERE entity_type = 'AttributeDefinition'
              AND locale = @locale
              AND is_active = true
              AND embedding IS NOT NULL
              AND embedded_content_hash = content_hash
            ORDER BY embedding <=> @query
            LIMIT @topK;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("query", new Vector(embedding));
        command.Parameters.AddWithValue("locale", locale);
        command.Parameters.AddWithValue("topK", topK);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SemanticAttributeCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SemanticAttributeCandidate(
                AttributeCode: reader.GetString(0),
                Name: reader.GetString(1),
                Similarity: reader.GetDouble(2)));
        }

        return results;
    }

    public async Task<IReadOnlyList<SemanticAttributeOptionCandidate>> SearchOptionsAsync(
        string attributeCode,
        ReadOnlyMemory<float> embedding,
        int topK,
        string locale,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT attribute_code, option_code, name, 1 - (embedding <=> @query) AS similarity
            FROM public.sku_attribute_embeddings
            WHERE entity_type = 'AttributeOption'
              AND attribute_code = @attributeCode
              AND locale = @locale
              AND is_active = true
              AND embedding IS NOT NULL
              AND embedded_content_hash = content_hash
            ORDER BY embedding <=> @query
            LIMIT @topK;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("query", new Vector(embedding));
        command.Parameters.AddWithValue("attributeCode", attributeCode);
        command.Parameters.AddWithValue("locale", locale);
        command.Parameters.AddWithValue("topK", topK);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SemanticAttributeOptionCandidate>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SemanticAttributeOptionCandidate(
                AttributeCode: reader.GetString(0),
                OptionCode: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Name: reader.GetString(2),
                Similarity: reader.GetDouble(3)));
        }

        return results;
    }
}
