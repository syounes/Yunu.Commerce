namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Input for synchronizing the pgvector projection of the active Segment
/// catalog (SegmentDefinitions + SegmentOptions). Both parameters are
/// optional: when <see cref="Provider"/> is omitted, the AI module's
/// configured DefaultProvider is used; when <see cref="BatchSize"/> is
/// omitted, the configured default batch size is used.
/// </summary>
public sealed record SynchronizeSegmentEmbeddingsCommand(
    string? Provider,
    int? BatchSize);
