using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Domain.Products;

namespace Yunu.Commerce.Catalog.Application.Brands.UpdateBrand;

public sealed class UpdateBrandHandler
{
    private readonly IBrandRepository _repository;
    private readonly IProductRepository _productRepository;

    public UpdateBrandHandler(IBrandRepository repository, IProductRepository productRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
    }

    public async Task HandleAsync(UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        var brandId = new BrandId(command.BrandId);
        var brand = await _repository.GetByIdAsync(brandId, cancellationToken);
        if (brand is null)
        {
            throw new KeyNotFoundException($"Brand '{command.BrandId}' not found.");
        }

        if (await _productRepository.ExistsByBrandIdAsync(brandId, cancellationToken))
        {
            throw new BrandInUseException($"Brand '{command.BrandId}' is used by at least one Product and cannot be updated.");
        }

        if (command.Name is { } newName)
        {
            brand.Rename(new BrandName(newName));
        }

        if (command.Status is { } statusString)
        {
            if (!Enum.TryParse<BrandStatus>(statusString, true, out var parsedStatus))
            {
                throw new ArgumentException($"Invalid status: {statusString}");
            }

            if (parsedStatus == BrandStatus.Active)
            {
                brand.Activate();
            }
            else
            {
                brand.Deactivate();
            }
        }

        await _repository.UpdateAsync(brand, cancellationToken);
    }
}
