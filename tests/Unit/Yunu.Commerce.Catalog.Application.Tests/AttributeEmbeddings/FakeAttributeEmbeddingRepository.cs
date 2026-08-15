using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeEmbeddings;

/// <summary>
/// Test-only in-memory fake for IAttributeEmbeddingRepository. Exists
/// exclusively inside this test project (docs task: "SKU attribute embedding
/// synchronization pipeline").
/// </summary>
internal sealed class FakeAttributeEmbeddingRepository : IAttributeEmbeddingRepository
{
    private readonly Dictionary<(string EntityType, string EntityId, string Locale), AttributeEmbeddingDocument> _rows = new();

    public IReadOnlyDictionary<(string EntityType, string EntityId, string Locale), AttributeEmbeddingDocument> Rows => _rows;

    public int UpsertCallCount { get; private set; }

    public void Seed(AttributeEmbeddingDocument document)
    {
        _rows[(document.EntityType, document.EntityId, document.Locale)] = document;
    }

    public Task<Guid> UpsertAsync(AttributeEmbeddingDocument document, CancellationToken cancellationToken = default)
    {
        UpsertCallCount++;

        var key = (document.EntityType, document.EntityId, document.Locale);

        var id = _rows.TryGetValue(key, out var existing) ? existing.Id : document.Id;

        _rows[key] = new AttributeEmbeddingDocument
        {
            Id = id,
            EntityType = document.EntityType,
            EntityId = document.EntityId,
            AttributeCode = document.AttributeCode,
            OptionCode = document.OptionCode,
            GoogleCategoryId = document.GoogleCategoryId,
            SkuId = document.SkuId,
            Locale = document.Locale,
            Name = document.Name,
            SemanticText = document.SemanticText,
            Embedding = document.Embedding,
            EmbeddingModel = document.EmbeddingModel,
            ContentHash = document.ContentHash,
            EmbeddedContentHash = document.EmbeddedContentHash,
            Metadata = document.Metadata,
            SourceUpdatedAt = document.SourceUpdatedAt,
            EmbeddedAt = document.EmbeddedAt,
            IsActive = document.IsActive
        };

        return Task.FromResult(id);
    }

    public Task<IReadOnlyCollection<AttributeEmbeddingMetadata>> GetMetadataByLocaleAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        var results = _rows.Values
            .Where(r => r.Locale == locale)
            .Select(r => new AttributeEmbeddingMetadata(
                r.EntityType,
                r.EntityId,
                r.Locale,
                r.ContentHash,
                r.EmbeddedContentHash,
                r.Embedding is not null))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AttributeEmbeddingMetadata>>(results);
    }
}
