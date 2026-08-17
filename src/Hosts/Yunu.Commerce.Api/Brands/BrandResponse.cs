namespace Yunu.Commerce.Api.Brands;

public sealed class BrandResponse
{
    public required System.Guid BrandId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string NormalizedName { get; init; }
    public required string Status { get; init; }
}
