namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when <see cref="SourceTaxonomyImportOrchestrator"/> is asked to
/// import a SourceTaxonomy that exists but is not active
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §9).
/// </summary>
public sealed class SourceTaxonomyInactiveException : Exception
{
    public SourceTaxonomyInactiveException(long sourceTaxonomyId)
        : base($"SourceTaxonomy {sourceTaxonomyId} is inactive and cannot be imported.")
    {
    }
}
