namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Thrown when an UPDATE or DELETE is attempted against a Canonical Taxonomy
/// (docs task: "Canonical Taxonomy + Segments Domain" §22).
/// </summary>
public sealed class CanonicalTaxonomyNodeInUseException : Exception
{
    public CanonicalTaxonomyNodeInUseException(string message) : base(message)
    {
    }
}
