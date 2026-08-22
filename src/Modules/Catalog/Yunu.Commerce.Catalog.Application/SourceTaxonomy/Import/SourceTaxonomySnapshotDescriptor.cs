namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Normalized, provider-neutral snapshot header returned by an
/// <see cref="ISourceTaxonomyAdapter"/> (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §5, §9). Represents the complete current state of one upstream taxonomy;
/// it is not a partial patch (ADR-0014 §6).
/// </summary>
public sealed record SourceTaxonomySnapshotDescriptor
{
    public required string ProviderCode { get; init; }
    public string? ScopeCode { get; init; }
    public string? ExternalTaxonomyId { get; init; }
    public string? ExternalVersion { get; init; }
    public required string Locale { get; init; }
    public string? SourceUri { get; init; }
    public string? SourceChecksum { get; init; }
}
