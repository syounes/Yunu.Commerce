using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Infrastructure.SegmentEmbeddings.PostgreSql;

/// <summary>
/// PostgreSQL + pgvector adapter implementing <see cref="ISegmentEmbeddingRepository"/>.
/// Uses plain Npgsql (no ORM) with the official Pgvector.Npgsql integration so
/// the embedding is persisted as a native "vector" column, mirroring
/// <see cref="Catalog.Infrastructure.AttributeEmbeddings.PostgreSql.PostgreSqlAttributeEmbeddingRepository"/>.
///
/// Persists to public.segment_embeddings
/// (deploy/databases/postgres/004_create_canonical_taxonomy_segment_vectors.sql,
/// deploy/databases/postgres/005-add-segment-assignment-scope.sql) reusing
/// the existing database functions/views:
/// public.upsert_segment_embedding_source, public.complete_segment_embedding
/// and public.pending_segment_embeddings. No business rule or SQL function is
/// duplicated in C#; this adapter only maps parameters and reads rows.
/// </summary>
public sealed class PostgreSqlSegmentEmbeddingRepository : ISegmentEmbeddingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlSegmentEmbeddingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<(string EntityType, long EntityId)>> GetExistingKeysAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT entity_type, entity_id
            FROM public.segment_embeddings
            WHERE locale = @locale;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("locale", locale);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<(string EntityType, long EntityId)>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        return results;
    }

    public async Task UpsertSourceAsync(SegmentEmbeddingSource source, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT public.upsert_segment_embedding_source(
                @p_entity_type,
                @p_entity_id,
                @p_segment_definition_id,
                @p_segment_code,
                @p_name,
                @p_semantic_text,
                @p_assignment_scope,
                @p_segment_option_id,
                @p_option_code,
                @p_locale,
                @p_metadata::jsonb,
                @p_source_updated_at
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("p_entity_type", source.EntityType);
        command.Parameters.AddWithValue("p_entity_id", source.EntityId);
        command.Parameters.AddWithValue("p_segment_definition_id", source.SegmentDefinitionId);
        command.Parameters.AddWithValue("p_segment_code", source.SegmentCode);
        command.Parameters.AddWithValue("p_name", source.Name);
        command.Parameters.AddWithValue("p_semantic_text", source.SemanticText);
        command.Parameters.AddWithValue("p_assignment_scope", source.AssignmentScope);
        command.Parameters.AddWithValue("p_segment_option_id", (object?)source.SegmentOptionId ?? DBNull.Value);
        command.Parameters.AddWithValue("p_option_code", (object?)source.OptionCode ?? DBNull.Value);
        command.Parameters.AddWithValue("p_locale", source.Locale);
        command.Parameters.Add(new NpgsqlParameter("p_metadata", NpgsqlDbType.Text) { Value = source.Metadata });
        command.Parameters.AddWithValue("p_source_updated_at", (object?)source.SourceUpdatedAt ?? DBNull.Value);

        await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<int> DeactivateMissingAsync(
        string locale,
        IReadOnlyCollection<(string EntityType, long EntityId)> activeKeys,
        CancellationToken cancellationToken = default)
    {
        var entityTypes = activeKeys.Select(k => k.EntityType).ToArray();
        var entityIds = activeKeys.Select(k => k.EntityId).ToArray();

        const string sql = """
            UPDATE public.segment_embeddings
            SET is_active = FALSE,
                updated_at = NOW()
            WHERE locale = @locale
              AND is_active
              AND NOT EXISTS (
                  SELECT 1
                  FROM UNNEST(@entity_types::varchar[], @entity_ids::bigint[]) AS active(entity_type, entity_id)
                  WHERE active.entity_type = segment_embeddings.entity_type
                    AND active.entity_id = segment_embeddings.entity_id
              );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("locale", locale);
        command.Parameters.Add(new NpgsqlParameter("entity_types", NpgsqlDbType.Array | NpgsqlDbType.Varchar) { Value = entityTypes });
        command.Parameters.Add(new NpgsqlParameter("entity_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint) { Value = entityIds });

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SegmentEmbeddingPendingItem>> GetPendingAsync(
        string locale,
        string provider,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT p.id, p.entity_type, p.entity_id, p.segment_definition_id, p.segment_option_id,
                   p.segment_code, p.option_code, p.locale, p.name, p.semantic_text, p.content_hash,
                   p.metadata, p.source_updated_at
            FROM public.pending_segment_embeddings p
            WHERE p.locale = @locale
            UNION
            SELECT e.id, e.entity_type, e.entity_id, e.segment_definition_id, e.segment_option_id,
                   e.segment_code, e.option_code, e.locale, e.name, e.semantic_text, e.content_hash,
                   e.metadata, e.source_updated_at
            FROM public.segment_embeddings e
            WHERE e.locale = @locale
              AND e.is_active
              AND e.embedding_provider IS DISTINCT FROM @provider;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("locale", locale);
        command.Parameters.AddWithValue("provider", provider);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<SegmentEmbeddingPendingItem>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SegmentEmbeddingPendingItem(
                Id: reader.GetGuid(0),
                EntityType: reader.GetString(1),
                EntityId: reader.GetInt64(2),
                SegmentDefinitionId: reader.GetInt64(3),
                SegmentOptionId: reader.IsDBNull(4) ? null : reader.GetInt64(4),
                SegmentCode: reader.GetString(5),
                OptionCode: reader.IsDBNull(6) ? null : reader.GetString(6),
                Locale: reader.GetString(7),
                Name: reader.GetString(8),
                SemanticText: reader.GetString(9),
                ContentHash: reader.GetString(10),
                Metadata: reader.IsDBNull(11) ? "{}" : reader.GetString(11),
                SourceUpdatedAt: reader.IsDBNull(12) ? null : reader.GetDateTime(12)));
        }

        return results;
    }

    public async Task<bool> CompleteAsync(
        string entityType,
        long entityId,
        string locale,
        string observedContentHash,
        string provider,
        string model,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT public.complete_segment_embedding(
                @p_entity_type,
                @p_entity_id,
                @p_locale,
                @p_content_hash,
                @p_embedding_provider,
                @p_embedding_model,
                @p_embedding
            );
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("p_entity_type", entityType);
        command.Parameters.AddWithValue("p_entity_id", entityId);
        command.Parameters.AddWithValue("p_locale", locale);
        command.Parameters.AddWithValue("p_content_hash", observedContentHash);
        command.Parameters.AddWithValue("p_embedding_provider", provider);
        command.Parameters.AddWithValue("p_embedding_model", model);
        command.Parameters.AddWithValue("p_embedding", new Vector(embedding));

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is bool completed && completed;
    }
}
