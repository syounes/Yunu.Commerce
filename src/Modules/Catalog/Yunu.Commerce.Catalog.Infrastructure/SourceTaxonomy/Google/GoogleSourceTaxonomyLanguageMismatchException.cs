namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Google;

/// <summary>
/// Raised when the persisted Google Product Taxonomy's SourceLanguage is
/// incompatible with the target SourceTaxonomy's DefaultLanguage
/// (docs/adr/0014-provider-neutral-source-taxonomy.md §9,
/// GoogleSourceTaxonomyAdapter §5). Import must fail instead of silently
/// importing a dataset in the wrong language as if it were the configured
/// source identity.
/// </summary>
public sealed class GoogleSourceTaxonomyLanguageMismatchException : Exception
{
    public GoogleSourceTaxonomyLanguageMismatchException(string expectedLanguage, string actualLanguage)
        : base($"Persisted Google taxonomy SourceLanguage '{actualLanguage}' is not compatible with the SourceTaxonomy DefaultLanguage '{expectedLanguage}'.")
    {
    }
}
