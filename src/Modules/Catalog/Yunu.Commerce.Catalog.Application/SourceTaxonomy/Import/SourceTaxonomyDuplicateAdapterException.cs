namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when more than one registered <see cref="ISourceTaxonomyAdapter"/>
/// matches the same AdapterCode (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §10). Adapter selection must remain deterministic; a duplicate
/// registration is a configuration failure, not a random selection.
/// </summary>
public sealed class SourceTaxonomyDuplicateAdapterException : Exception
{
    public SourceTaxonomyDuplicateAdapterException(string adapterCode)
        : base($"More than one SourceTaxonomy adapter is registered for AdapterCode '{adapterCode}'.")
    {
    }
}
