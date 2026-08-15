namespace Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

/// <summary>
/// Raised when a SKU attribute embedding synchronization is requested while
/// another one is already running. The API layer translates this into HTTP 409.
/// </summary>
public sealed class AttributeEmbeddingSynchronizationInProgressException : Exception
{
    public AttributeEmbeddingSynchronizationInProgressException()
        : base("A SKU attribute embedding synchronization is already running.")
    {
    }
}
