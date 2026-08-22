namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when <see cref="SourceTaxonomyImportOrchestrator"/> is asked to
/// import a SourceTaxonomyId that does not exist
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §9).
/// </summary>
public sealed class SourceTaxonomyNotFoundException : Exception
{
    public SourceTaxonomyNotFoundException(long sourceTaxonomyId)
        : base($"SourceTaxonomy {sourceTaxonomyId} was not found.")
    {
    }
}
