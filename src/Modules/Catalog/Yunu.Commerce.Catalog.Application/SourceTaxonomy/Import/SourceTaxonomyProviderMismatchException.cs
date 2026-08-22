namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when a snapshot's ProviderCode does not match the persisted
/// SourceTaxonomy's ProviderCode (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §8). Import must never silently transform one source's provider
/// identity into another; ProviderCode is the identity boundary.
/// </summary>
public sealed class SourceTaxonomyProviderMismatchException : Exception
{
    public SourceTaxonomyProviderMismatchException(string existingProviderCode, string snapshotProviderCode)
        : base($"Snapshot ProviderCode '{snapshotProviderCode}' does not match the existing SourceTaxonomy ProviderCode '{existingProviderCode}'.")
    {
    }
}
