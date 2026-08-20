namespace Yunu.Commerce.Catalog.Application.Products;

/// <summary>
/// Thrown when a Product lifecycle Status transition command cannot be
/// applied because the Aggregate was concurrently changed by another writer
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
///
/// A transition command operates on the state view loaded for that attempt
/// only; when the conditional persistence write does not match (someone
/// else already changed the Product's Status), the command does NOT reload
/// and reinterpret its original intention against the new state. It fails
/// explicitly instead, following a first-writer-wins policy. The HTTP layer
/// translates this into 409 Conflict.
/// </summary>
public sealed class ProductStatusConcurrencyConflictException : Exception
{
    public ProductStatusConcurrencyConflictException(string message) : base(message)
    {
    }
}
