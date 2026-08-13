namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// HTTP response contract returned after a Sku is created.
/// </summary>
public sealed class CreateSkuResponse
{
    public required Guid SkuId { get; init; }
}
