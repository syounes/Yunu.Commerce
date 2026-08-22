namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Provider-neutral input passed to <see cref="ISourceTaxonomyAdapter.LoadAsync"/>
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §9). Built by the
/// generic orchestrator from the currently persisted
/// <see cref="SourceTaxonomyDescriptorRecord"/>; adapters use it to load the
/// correct upstream taxonomy without any coupling to SQL persistence,
/// repository objects or Canonical/Google-specific identifiers.
/// </summary>
public sealed record SourceTaxonomyImportContext
{
    public required long SourceTaxonomyId { get; init; }
    public required string Code { get; init; }
    public required string ProviderCode { get; init; }
    public string? ScopeCode { get; init; }
    public string? ExternalTaxonomyId { get; init; }
    public string? CurrentExternalVersion { get; init; }
    public required string DefaultLanguage { get; init; }
    public string? SourceUri { get; init; }
    public string? CurrentSourceChecksum { get; init; }
}
