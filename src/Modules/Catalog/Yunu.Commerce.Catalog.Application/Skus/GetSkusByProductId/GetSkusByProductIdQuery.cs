namespace Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;

/// <summary>
/// Input for retrieving all Skus belonging to a given Product identity.
/// </summary>
public sealed class GetSkusByProductIdQuery
{
    public required Guid ProductId { get; init; }
}
