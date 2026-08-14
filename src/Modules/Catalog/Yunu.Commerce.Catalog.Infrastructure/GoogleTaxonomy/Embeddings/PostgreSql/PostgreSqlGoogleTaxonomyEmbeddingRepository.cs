using Npgsql;
using Pgvector;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.GenerateGoogleTaxonomyEmbedding;

namespace Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Embeddings.PostgreSql;

/// <summary>
/// PostgreSQL + pgvector adapter implementing <see cref="IGoogleTaxonomyEmbeddingRepository"/>.
/// Uses plain Npgsql (no ORM) with the official Pgvector.Npgsql integration so
/// the embedding is persisted as a native "vector" column rather than a
/// string/JSON/byte[] encoding (docs task: "GenerateGoogleTaxonomyEmbedding").
///
/// Persists to google_taxonomy_embeddings
/// (deploy/docker/postgres/init/002-create-google-taxonomy-embeddings.sql) using
/// an atomic INSERT ... ON CONFLICT DO UPDATE upsert keyed by
/// (google_category_id, provider, model).
/// </summary>
public sealed class PostgreSqlGoogleTaxonomyEmbeddingRepository : IGoogleTaxonomyEmbeddingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlGoogleTaxonomyEmbeddingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Guid> UpsertAsync(GoogleTaxonomyEmbedding embedding, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO google_taxonomy_embeddings
                (id, google_category_id, category_path, provider, model, dimensions, embedding, created_at_utc, updated_at_utc)
            VALUES
                (@id, @google_category_id, @category_path, @provider, @model, @dimensions, @embedding, @created_at_utc, @updated_at_utc)
            ON CONFLICT (google_category_id, provider, model)
            DO UPDATE SET
                category_path = EXCLUDED.category_path,
                dimensions = EXCLUDED.dimensions,
                embedding = EXCLUDED.embedding,
                updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", embedding.Id);
        command.Parameters.AddWithValue("google_category_id", embedding.GoogleCategoryId);
        command.Parameters.AddWithValue("category_path", embedding.CategoryPath);
        command.Parameters.AddWithValue("provider", embedding.Provider);
        command.Parameters.AddWithValue("model", embedding.Model);
        command.Parameters.AddWithValue("dimensions", embedding.Dimensions);
        command.Parameters.AddWithValue("embedding", new Vector(embedding.Embedding));
        command.Parameters.AddWithValue("created_at_utc", embedding.CreatedAtUtc);
        command.Parameters.AddWithValue("updated_at_utc", embedding.UpdatedAtUtc);

        var persistedId = await command.ExecuteScalarAsync(cancellationToken);

        return (Guid)persistedId!;
    }

    public async Task<IReadOnlyCollection<GoogleTaxonomyEmbeddingMetadata>> GetMetadataByProviderAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT google_category_id, provider, model, category_path
            FROM google_taxonomy_embeddings
            WHERE provider = @provider;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("provider", provider);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleTaxonomyEmbeddingMetadata>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GoogleTaxonomyEmbeddingMetadata(
                GoogleCategoryId: reader.GetInt32(0),
                Provider: reader.GetString(1),
                Model: reader.GetString(2),
                CategoryPath: reader.GetString(3)));
        }

        return results;
    }
}
