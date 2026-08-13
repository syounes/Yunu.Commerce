namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Raised when a downloaded Google Product Taxonomy feed fails structural
/// validation (empty feed, duplicate IDs, duplicate paths, missing parents,
/// hierarchy cycles). Synchronization must not write to SQL Server when this
/// exception is thrown.
/// </summary>
public sealed class GoogleTaxonomyValidationException : Exception
{
    public GoogleTaxonomyValidationException(string message) : base(message)
    {
    }
}
