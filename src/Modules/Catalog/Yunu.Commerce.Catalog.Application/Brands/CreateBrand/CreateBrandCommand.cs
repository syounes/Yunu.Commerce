namespace Yunu.Commerce.Catalog.Application.Brands.CreateBrand;

public sealed class CreateBrandCommand
{
    public required string Code { get; init; }

    public required string Name { get; init; }
}
