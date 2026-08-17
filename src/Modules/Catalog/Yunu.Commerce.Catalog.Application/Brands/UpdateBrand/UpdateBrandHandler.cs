using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.UpdateBrand;

public sealed class UpdateBrandHandler
{
    private readonly IBrandRepository _repository;

    public UpdateBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        var brandId = new BrandId(command.BrandId);
        var brand = await _repository.GetByIdAsync(brandId, cancellationToken);
        if (brand is null)
        {
            throw new KeyNotFoundException($"Brand '{command.BrandId}' not found.");
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
