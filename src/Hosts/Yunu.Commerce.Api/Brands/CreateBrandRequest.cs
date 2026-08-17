namespace Yunu.Commerce.Api.Brands;

public sealed class CreateBrandRequest
{
    public required string Code { get; init; }
    public required string Name { get; init; }
}
