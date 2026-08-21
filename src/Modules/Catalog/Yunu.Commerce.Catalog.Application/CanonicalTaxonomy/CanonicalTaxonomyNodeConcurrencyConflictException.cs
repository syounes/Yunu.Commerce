namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;

/// <summary>
/// Thrown when a Canonical Taxonomy mutation (rename/update, lifecycle
/// transition, or child creation participating in the parent's revision)
/// cannot be applied because the persisted state was concurrently changed
/// by another writer (docs task: "Yunu.Commerce - Canonical Taxonomy
/// Concurrency Guard"), mirroring
/// <see cref="Yunu.Commerce.Catalog.Application.Products.ProductStatusConcurrencyConflictException"/>.
///
/// A mutation command operates on the state view it loaded for that attempt
/// only; when the conditional persistence write does not match (someone
/// else already changed the node's persisted Revision), the command does
/// NOT reload and reinterpret its original intention against the new
/// state. It fails explicitly instead, following a first-writer-wins
/// policy. No automatic retry is performed.
/// </summary>
public sealed class CanonicalTaxonomyNodeConcurrencyConflictException : Exception
{
    public CanonicalTaxonomyNodeConcurrencyConflictException(string message) : base(message)
    {
    }
}
