using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.PostgreSql;

/// <summary>
/// PostgreSQL + pgvector adapter implementing <see cref="IAttributeEmbeddingRepository"/>.
/// Uses plain Npgsql (no ORM) with the official Pgvector.Npgsql integration so
/// the embedding is persisted as a native "vector" column, mirroring
/// <see cref="Catalog.Infrastructure.GoogleTaxonomy.Embeddings.PostgreSql.PostgreSqlGoogleTaxonomyEmbeddingRepository"/>.
///
/// Persists to public.sku_attribute_embeddings
/// (deploy/docker/postgres/init/003_create_sku_attribute_vectors.sql) using an
/// atomic INSERT ... ON CONFLICT DO UPDATE upsert keyed by
/// (entity_type, entity_id, locale). The PostgreSQL-generated id is preserved
/// during updates (id is only used in the INSERT branch/RETURNING clause).
/// </summary>
public sealed class PostgreSqlAttributeEmbeddingRepository : IAttributeEmbeddingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlAttributeEmbeddingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Guid> UpsertAsync(AttributeEmbeddingDocument document, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.sku_attribute_embeddings
                (id, entity_type, entity_id, attribute_code, option_code, google_category_id, sku_id,
                 locale, name, semantic_text, embedding, embedding_model, content_hash, embedded_content_hash,
                 metadata, source_updated_at, embedded_at, is_active, created_at, updated_at)
            VALUES
                (@id, @entity_type, @entity_id, @attribute_code, @option_code, @google_category_id, @sku_id,
                 @locale, @name, @semantic_text, @embedding, @embedding_model, @content_hash, @embedded_content_hash,
                 @metadata::jsonb, @source_updated_at, @embedded_at, @is_active, now(), now())
            ON CONFLICT (entity_type, entity_id, locale)
            DO UPDATE SET
                attribute_code = EXCLUDED.attribute_code,
                option_code = EXCLUDED.option_code,
                name = EXCLUDED.name,
                semantic_text = EXCLUDED.semantic_text,
                embedding = EXCLUDED.embedding,
                embedding_model = EXCLUDED.embedding_model,
                content_hash = EXCLUDED.content_hash,
                embedded_content_hash = EXCLUDED.embedded_content_hash,
                metadata = EXCLUDED.metadata,
                source_updated_at = EXCLUDED.source_updated_at,
                embedded_at = EXCLUDED.embedded_at,
                is_active = EXCLUDED.is_active,
                updated_at = now()
            RETURNING id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", document.Id);
        command.Parameters.AddWithValue("entity_type", document.EntityType);
        command.Parameters.AddWithValue("entity_id", document.EntityId);
        command.Parameters.AddWithValue("attribute_code", document.AttributeCode);
        command.Parameters.AddWithValue("option_code", (object?)document.OptionCode ?? DBNull.Value);
        command.Parameters.AddWithValue("google_category_id", (object?)document.GoogleCategoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("sku_id", (object?)document.SkuId ?? DBNull.Value);
        command.Parameters.AddWithValue("locale", document.Locale);
        command.Parameters.AddWithValue("name", document.Name);
        command.Parameters.AddWithValue("semantic_text", document.SemanticText);
        command.Parameters.AddWithValue("embedding", (object?)(document.Embedding is null ? null : new Vector(document.Embedding)) ?? DBNull.Value);
        command.Parameters.AddWithValue("embedding_model", (object?)document.EmbeddingModel ?? DBNull.Value);
        command.Parameters.AddWithValue("content_hash", document.ContentHash);
        command.Parameters.AddWithValue("embedded_content_hash", (object?)document.EmbeddedContentHash ?? DBNull.Value);
        command.Parameters.Add(new NpgsqlParameter("metadata", NpgsqlDbType.Text) { Value = document.Metadata });
        command.Parameters.AddWithValue("source_updated_at", (object?)document.SourceUpdatedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("embedded_at", (object?)document.EmbeddedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("is_active", document.IsActive);

        var persistedId = await command.ExecuteScalarAsync(cancellationToken);

        return (Guid)persistedId!;
    }

    public async Task<IReadOnlyCollection<AttributeEmbeddingMetadata>> GetMetadataByLocaleAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT entity_type, entity_id, locale, content_hash, embedded_content_hash, (embedding IS NOT NULL)
            FROM public.sku_attribute_embeddings
            WHERE locale = @locale;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("locale", locale);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<AttributeEmbeddingMetadata>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AttributeEmbeddingMetadata(
                EntityType: reader.GetString(0),
                EntityId: reader.GetString(1),
                Locale: reader.GetString(2),
                ContentHash: reader.GetString(3),
                EmbeddedContentHash: reader.IsDBNull(4) ? null : reader.GetString(4),
                HasEmbedding: reader.GetBoolean(5)));
        }

        return results;
    }
}
