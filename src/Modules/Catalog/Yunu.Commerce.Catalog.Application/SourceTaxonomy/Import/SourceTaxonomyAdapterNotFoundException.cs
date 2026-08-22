namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when no registered <see cref="ISourceTaxonomyAdapter"/> matches the
/// requested AdapterCode (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §10). Adapter resolution never falls back to provider-specific branching.
/// </summary>
public sealed class SourceTaxonomyAdapterNotFoundException : Exception
{
    public SourceTaxonomyAdapterNotFoundException(string adapterCode)
        : base($"No SourceTaxonomy adapter registered for AdapterCode '{adapterCode}'.")
    {
    }
}
