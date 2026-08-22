namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Google;

/// <summary>
/// Raised when the persisted Google Product Taxonomy dataset contains
/// conflicting SourceLanguage values across rows
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §9,
/// GoogleSourceTaxonomyAdapter §5). A SourceTaxonomySnapshot represents a
/// single Locale; a Google dataset spanning multiple languages cannot be
/// normalized deterministically into one snapshot.
/// </summary>
public sealed class GoogleSourceTaxonomyInconsistentLanguageException : Exception
{
    public GoogleSourceTaxonomyInconsistentLanguageException(string firstLanguage, string conflictingLanguage)
        : base($"Persisted Google taxonomy dataset contains conflicting SourceLanguage values ('{firstLanguage}' vs '{conflictingLanguage}'). A SourceTaxonomySnapshot requires a single, consistent locale.")
    {
    }
}
