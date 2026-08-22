namespace Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

/// <summary>
/// Raised when a normalized <see cref="SourceTaxonomySnapshot"/> fails
/// structural validation before any catalog mutation is attempted
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §7). Validation is
/// intentionally provider-neutral: no provider-specific rule is enforced
/// here.
/// </summary>
public sealed class SourceTaxonomySnapshotValidationException : Exception
{
    public SourceTaxonomySnapshotValidationException(string message) : base(message)
    {
    }
}
