namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Normalized, provider-neutral snapshot of one complete upstream taxonomy
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §5, §6, §9). Produced
/// by an <see cref="ISourceTaxonomyAdapter"/> and validated by
/// <see cref="SourceTaxonomySnapshotValidator"/> before any catalog mutation
/// occurs.
/// </summary>
public sealed record SourceTaxonomySnapshot
{
    public required SourceTaxonomySnapshotDescriptor Descriptor { get; init; }
    public required IReadOnlyCollection<SourceTaxonomySnapshotNode> Nodes { get; init; }
}
