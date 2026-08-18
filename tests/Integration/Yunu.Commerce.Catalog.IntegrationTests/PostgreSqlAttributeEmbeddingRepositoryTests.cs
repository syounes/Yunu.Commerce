using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;
using Yunu.Commerce.Catalog.Infrastructure.AttributeEmbeddings.PostgreSql;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests for PostgreSqlAttributeEmbeddingRepository against a real
/// PostgreSQL + pgvector instance via Testcontainers (docs task: "SKU
/// attribute embedding synchronization pipeline"). The schema is created by
/// executing deploy/databases/postgres/003_create_sku_attribute_vectors.sql
/// directly against the container.
/// </summary>
public sealed class PostgreSqlAttributeEmbeddingRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();
    private NpgsqlDataSource _dataSource = null!;
    private PostgreSqlAttributeEmbeddingRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        await using (var setupConnection = new NpgsqlConnection(_postgresContainer.GetConnectionString()))
        {
            await setupConnection.OpenAsync();
            await using var extensionCommand = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", setupConnection);
            await extensionCommand.ExecuteNonQueryAsync();
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_postgresContainer.GetConnectionString());
        dataSourceBuilder.UseVector();
        _dataSource = dataSourceBuilder.Build();

        await CreateSchemaAsync();

        _repository = new PostgreSqlAttributeEmbeddingRepository(_dataSource);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    private async Task CreateSchemaAsync()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "deploy", "databases", "postgres", "003_create_sku_attribute_vectors.sql");

        var script = await File.ReadAllTextAsync(Path.GetFullPath(scriptPath));

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static AttributeEmbeddingDocument CreateDefinitionDocument(
        string entityId = "color",
        float[]? embedding = null,
        string? embeddedContentHash = null,
        DateTime? embeddedAt = null)
    {
        var semanticText = $"Atributo: Cor. Código: {entityId}.";
        var contentHash = AttributeSemanticDocumentBuilder.ComputeContentHash(semanticText);

        return new AttributeEmbeddingDocument
        {
            Id = Guid.NewGuid(),
            EntityType = "AttributeDefinition",
            EntityId = entityId,
            AttributeCode = entityId,
            OptionCode = null,
            Locale = "pt-BR",
            Name = "Cor",
            SemanticText = semanticText,
            Embedding = embedding,
            EmbeddingModel = embedding is null ? null : "yunu-embedding-category-v1",
            ContentHash = contentHash,
            EmbeddedContentHash = embeddedContentHash,
            Metadata = "{\"attributeDefinitionId\":14}",
            SourceUpdatedAt = DateTime.UtcNow,
            EmbeddedAt = embeddedAt,
            IsActive = true
        };
    }

    [Fact]
    public async Task UpsertAsync_Should_Insert_New_Row_When_No_Existing_Match()
    {
        var document = CreateDefinitionDocument("upsert-new");

        var id = await _repository.UpsertAsync(document, CancellationToken.None);

        Assert.Equal(document.Id, id);
    }

    [Fact]
    public async Task UpsertAsync_Should_Upsert_By_EntityType_EntityId_And_Locale_And_Preserve_Existing_Id()
    {
        var first = CreateDefinitionDocument("preserve-id");
        var firstId = await _repository.UpsertAsync(first, CancellationToken.None);

        var second = CreateDefinitionDocument("preserve-id", embedding: new float[1536]);
        // Simulate a different generated Id, as the handler always assigns Guid.NewGuid() for a fresh document.
        second = new AttributeEmbeddingDocument
        {
            Id = Guid.NewGuid(),
            EntityType = second.EntityType,
            EntityId = second.EntityId,
            AttributeCode = second.AttributeCode,
            OptionCode = second.OptionCode,
            Locale = second.Locale,
            Name = second.Name,
            SemanticText = second.SemanticText,
            Embedding = second.Embedding,
            EmbeddingModel = second.EmbeddingModel,
            ContentHash = second.ContentHash,
            EmbeddedContentHash = second.ContentHash,
            Metadata = second.Metadata,
            SourceUpdatedAt = second.SourceUpdatedAt,
            EmbeddedAt = DateTime.UtcNow,
            IsActive = true
        };

        var secondId = await _repository.UpsertAsync(second, CancellationToken.None);

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task UpsertAsync_Should_Write_Vector_And_Model()
    {
        var embedding = Enumerable.Range(0, 1536).Select(i => (float)i / 1536).ToArray();
        var document = CreateDefinitionDocument("vector-write", embedding: embedding, embeddedContentHash: null);

        document = new AttributeEmbeddingDocument
        {
            Id = document.Id,
            EntityType = document.EntityType,
            EntityId = document.EntityId,
            AttributeCode = document.AttributeCode,
            OptionCode = document.OptionCode,
            Locale = document.Locale,
            Name = document.Name,
            SemanticText = document.SemanticText,
            Embedding = embedding,
            EmbeddingModel = "yunu-embedding-category-v1",
            ContentHash = document.ContentHash,
            EmbeddedContentHash = document.ContentHash,
            Metadata = document.Metadata,
            SourceUpdatedAt = document.SourceUpdatedAt,
            EmbeddedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _repository.UpsertAsync(document, CancellationToken.None);

        var metadata = await _repository.GetMetadataByLocaleAsync("pt-BR", CancellationToken.None);
        var row = metadata.Single(m => m.EntityId == "vector-write");

        Assert.True(row.HasEmbedding);
        Assert.Equal(document.ContentHash, row.EmbeddedContentHash);
    }

    [Fact]
    public async Task GetMetadataByLocaleAsync_Should_Detect_Unchanged_Content()
    {
        var document = CreateDefinitionDocument("unchanged-content", embedding: new float[1536]);
        document = new AttributeEmbeddingDocument
        {
            Id = document.Id,
            EntityType = document.EntityType,
            EntityId = document.EntityId,
            AttributeCode = document.AttributeCode,
            OptionCode = document.OptionCode,
            Locale = document.Locale,
            Name = document.Name,
            SemanticText = document.SemanticText,
            Embedding = document.Embedding,
            EmbeddingModel = "yunu-embedding-category-v1",
            ContentHash = document.ContentHash,
            EmbeddedContentHash = document.ContentHash,
            Metadata = document.Metadata,
            SourceUpdatedAt = document.SourceUpdatedAt,
            EmbeddedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _repository.UpsertAsync(document, CancellationToken.None);

        var metadata = await _repository.GetMetadataByLocaleAsync("pt-BR", CancellationToken.None);
        var row = metadata.Single(m => m.EntityId == "unchanged-content");

        Assert.Equal(row.ContentHash, row.EmbeddedContentHash);
        Assert.True(row.HasEmbedding);
    }

    [Fact]
    public async Task GetMetadataByLocaleAsync_Should_Read_Existing_Seeded_Rows()
    {
        var metadata = await _repository.GetMetadataByLocaleAsync("pt-BR", CancellationToken.None);

        Assert.NotEmpty(metadata);
        Assert.Contains(metadata, m => m.EntityType == "AttributeDefinition" && m.EntityId == "color");
        Assert.All(metadata, m => Assert.False(m.HasEmbedding));
    }
}
