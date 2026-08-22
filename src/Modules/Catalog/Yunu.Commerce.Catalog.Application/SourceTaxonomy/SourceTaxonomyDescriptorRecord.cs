namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy;

/// <summary>
/// Provider-neutral read model for a persisted SourceTaxonomy header
/// (docs/adr/0014-provider-neutral-source-taxonomy.md).
/// Mirrors the columns of Catalog.SourceTaxonomies
/// (deploy/databases/sqlserver/014-create-source-taxonomy-foundation.sql).
/// This is imported/reference data, not a user-managed business aggregate;
/// no domain lifecycle or mutation semantics are attached to it.
/// </summary>
public sealed record SourceTaxonomyDescriptorRecord
{
    public required long SourceTaxonomyId { get; init; }
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
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required DateTime ImportedAt { get; init; }
}
