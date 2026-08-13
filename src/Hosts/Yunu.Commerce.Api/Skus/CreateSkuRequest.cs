namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// HTTP request contract for creating a Sku. SkuId is intentionally absent:
/// identity is generated inside Catalog.Application.
/// </summary>
public sealed class CreateSkuRequest
{
    public required string Code { get; init; }

    public string? Gtin { get; init; }
}
