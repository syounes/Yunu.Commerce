namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when a SourceTaxonomy import is requested while another import of
/// the SAME SourceTaxonomyId is already running
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §18).
/// </summary>
public sealed class SourceTaxonomyImportInProgressException : Exception
{
    public SourceTaxonomyImportInProgressException(long sourceTaxonomyId)
        : base($"An import for SourceTaxonomy {sourceTaxonomyId} is already running.")
    {
    }
}
