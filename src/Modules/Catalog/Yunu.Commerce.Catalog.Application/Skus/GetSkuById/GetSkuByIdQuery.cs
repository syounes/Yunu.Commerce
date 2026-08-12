namespace Yunu.Commerce.Catalog.Application.Skus.GetSkuById;

/// <summary>
/// Input for retrieving a single Sku by identity.
/// </summary>
public sealed class GetSkuByIdQuery
{
    public required Guid SkuId { get; init; }
}
