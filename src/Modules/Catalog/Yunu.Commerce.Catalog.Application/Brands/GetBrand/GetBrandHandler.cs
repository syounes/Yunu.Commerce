using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.GetBrand;

public sealed class GetBrandHandler
{
    private readonly IBrandRepository _repository;

    public GetBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Brand?> Handle(GetBrandQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(query.BrandId, cancellationToken);
    }
}
