using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Catalog.Application.Tests.SegmentEmbeddings;

/// <summary>
/// Test-only in-memory fake for ISegmentEmbeddingRepository, simulating the
/// two-phase PostgreSQL projection behavior (upsert source, deactivate
/// missing, pending, optimistic completion) described by
/// deploy/databases/postgres/004_create_canonical_taxonomy_segment_vectors.sql
/// and deploy/databases/postgres/005-add-segment-assignment-scope.sql. Exists
/// exclusively inside this test project (docs task: "Implementar
/// sincronização de embeddings de segmentos").
/// </summary>
internal sealed class FakeSegmentEmbeddingRepository : ISegmentEmbeddingRepository
{
    public sealed class Row
    {
        public Guid Id { get; init; }
        public required string EntityType { get; set; }
        public required long EntityId { get; set; }
        public required long SegmentDefinitionId { get; set; }
        public long? SegmentOptionId { get; set; }
        public required string SegmentCode { get; set; }
        public string? OptionCode { get; set; }
        public required string AssignmentScope { get; set; }
        public required string Locale { get; set; }
        public required string Name { get; set; }
        public required string SemanticText { get; set; }
        public required string ContentHash { get; set; }
        public string? EmbeddedContentHash { get; set; }
        public float[]? Embedding { get; set; }
        public string? EmbeddingProvider { get; set; }
        public string? EmbeddingModel { get; set; }
        public required string Metadata { get; set; }
        public DateTime? SourceUpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private readonly Dictionary<(string EntityType, long EntityId, string Locale), Row> _rows = new();

    public IReadOnlyDictionary<(string EntityType, long EntityId, string Locale), Row> Rows => _rows;

    public int CompleteCallCount { get; private set; }

    /// <summary>
    /// When set, the content_hash of the targeted row is mutated immediately
    /// before the optimistic completion comparison runs, simulating a source
    /// change that happened while the provider call was in flight.
    /// </summary>
    public (string EntityType, long EntityId, string Locale)? SimulateRaceOnNextCompleteKey { get; set; }

    public void Seed(Row row)
    {
        _rows[(row.EntityType, row.EntityId, row.Locale)] = row;
    }

    public Task<IReadOnlyCollection<(string EntityType, long EntityId)>> GetExistingKeysAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        var results = _rows.Values
            .Where(r => r.Locale == locale)
            .Select(r => (r.EntityType, r.EntityId))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<(string EntityType, long EntityId)>>(results);
    }

    public Task UpsertSourceAsync(SegmentEmbeddingSource source, CancellationToken cancellationToken = default)
    {
        var key = (source.EntityType, source.EntityId, source.Locale);
        var contentHash = SegmentSemanticDocumentBuilder.ComputeContentHash(source.SemanticText);

        if (_rows.TryGetValue(key, out var existing))
        {
            existing.SegmentDefinitionId = source.SegmentDefinitionId;
            existing.SegmentOptionId = source.SegmentOptionId;
            existing.SegmentCode = source.SegmentCode;
            existing.OptionCode = source.OptionCode;
            existing.AssignmentScope = source.AssignmentScope;
            existing.Name = source.Name;
            existing.SemanticText = source.SemanticText;
            existing.ContentHash = contentHash;
            existing.Metadata = source.Metadata;
            existing.SourceUpdatedAt = source.SourceUpdatedAt;
            existing.IsActive = true;
        }
        else
        {
            _rows[key] = new Row
            {
                Id = Guid.NewGuid(),
                EntityType = source.EntityType,
                EntityId = source.EntityId,
                SegmentDefinitionId = source.SegmentDefinitionId,
                SegmentOptionId = source.SegmentOptionId,
                SegmentCode = source.SegmentCode,
                OptionCode = source.OptionCode,
                AssignmentScope = source.AssignmentScope,
                Locale = source.Locale,
                Name = source.Name,
                SemanticText = source.SemanticText,
                ContentHash = contentHash,
                Metadata = source.Metadata,
                SourceUpdatedAt = source.SourceUpdatedAt,
                IsActive = true
            };
        }

        return Task.CompletedTask;
    }

    public Task<int> DeactivateMissingAsync(
        string locale,
        IReadOnlyCollection<(string EntityType, long EntityId)> activeKeys,
        CancellationToken cancellationToken = default)
    {
        var activeSet = new HashSet<(string EntityType, long EntityId)>(activeKeys);
        var deactivated = 0;

        foreach (var row in _rows.Values.Where(r => r.Locale == locale && r.IsActive))
        {
            if (!activeSet.Contains((row.EntityType, row.EntityId)))
            {
                row.IsActive = false;
                deactivated++;
            }
        }

        return Task.FromResult(deactivated);
    }

    public Task<IReadOnlyCollection<SegmentEmbeddingPendingItem>> GetPendingAsync(
        string locale,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var results = _rows.Values
            .Where(r => r.Locale == locale && r.IsActive)
            .Where(r => r.Embedding is null
                        || r.EmbeddedContentHash != r.ContentHash
                        || r.EmbeddingProvider != provider)
            .Select(r => new SegmentEmbeddingPendingItem(
                r.Id,
                r.EntityType,
                r.EntityId,
                r.SegmentDefinitionId,
                r.SegmentOptionId,
                r.SegmentCode,
                r.OptionCode,
                r.Locale,
                r.Name,
                r.SemanticText,
                r.ContentHash,
                r.Metadata,
                r.SourceUpdatedAt))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<SegmentEmbeddingPendingItem>>(results);
    }

    public Task<bool> CompleteAsync(
        string entityType,
        long entityId,
        string locale,
        string observedContentHash,
        string provider,
        string model,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        CompleteCallCount++;

        var key = (entityType, entityId, locale);

        if (SimulateRaceOnNextCompleteKey.HasValue && SimulateRaceOnNextCompleteKey.Value == key)
        {
            SimulateRaceOnNextCompleteKey = null;

            if (_rows.TryGetValue(key, out var raced))
            {
                raced.ContentHash = Guid.NewGuid().ToString("N").PadRight(64, '0')[..64];
            }
        }

        if (!_rows.TryGetValue(key, out var row) || !row.IsActive || row.ContentHash != observedContentHash)
        {
            return Task.FromResult(false);
        }

        row.Embedding = embedding;
        row.EmbeddingProvider = provider;
        row.EmbeddingModel = model;
        row.EmbeddedContentHash = observedContentHash;

        return Task.FromResult(true);
    }
}
