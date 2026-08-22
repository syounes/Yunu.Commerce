namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy;

/// <summary>
/// Provider-neutral input required to internally create one
/// Catalog.SourceTaxonomies row (docs/adr/0014-provider-neutral-source-taxonomy.md).
/// SourceTaxonomyId and CreatedAt are database-generated and therefore not
/// supplied here. UpdatedAt is intentionally absent: it remains null on
/// initial creation.
/// </summary>
public sealed record SourceTaxonomyCreateRecord
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ProviderCode { get; init; }
    public string? ScopeCode { get; init; }
    public string? ExternalTaxonomyId { get; init; }
    public string? ExternalVersion { get; init; }
    public required string DefaultLanguage { get; init; }
    public string? SourceUri { get; init; }
    public string? SourceChecksum { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime ImportedAt { get; init; }
}
