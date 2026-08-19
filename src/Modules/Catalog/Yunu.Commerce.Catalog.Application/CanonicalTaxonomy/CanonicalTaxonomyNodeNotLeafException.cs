namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Thrown when an UPDATE or DELETE is attempted against a Canonical Taxonomy
/// task: "Canonical Taxonomy + Segments Domain" §22).
/// </summary>
public sealed class CanonicalTaxonomyNodeNotLeafException : Exception
{
    public CanonicalTaxonomyNodeNotLeafException(string message) : base(message)
    {
    }
}
