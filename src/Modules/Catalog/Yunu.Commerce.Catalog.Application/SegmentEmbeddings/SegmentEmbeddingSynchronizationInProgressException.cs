namespace Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

/// <summary>
/// Raised when a Segment embedding synchronization is requested while
/// another one is already running. The API layer translates this into HTTP 409.
/// </summary>
public sealed class SegmentEmbeddingSynchronizationInProgressException : Exception
{
    public SegmentEmbeddingSynchronizationInProgressException()
        : base("A Segment embedding synchronization is already running.")
    {
    }
}
