using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.CreateBrand;

public sealed class CreateBrandHandler
{
    private readonly IBrandRepository _repository;

    public CreateBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateBrandResult> HandleAsync(CreateBrandCommand command, CancellationToken cancellationToken)
    {
        var code = new BrandCode(command.Code);
        if (await _repository.ExistsCodeAsync(code, cancellationToken))
        {
            throw new InvalidOperationException($"Brand with code {code.Value} already exists.");
        }

        var name = new BrandName(command.Name);
        var id = BrandId.New();
        var brand = Brand.Create(id, code, name);

        await _repository.AddAsync(brand, cancellationToken);

        return new CreateBrandResult { BrandId = id.Value };
    }
}
