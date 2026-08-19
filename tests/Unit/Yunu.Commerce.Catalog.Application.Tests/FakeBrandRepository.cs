using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Tests;

internal sealed class FakeBrandRepository : IBrandRepository
{
    private readonly Dictionary<Guid, Brand> _brands = new();

    public Task AddAsync(Brand brand, CancellationToken cancellationToken)
    {
        _brands[brand.Id.Value] = brand;
        return Task.CompletedTask;
    }

    public Task<Brand?> GetByIdAsync(BrandId id, CancellationToken cancellationToken)
    {
        _brands.TryGetValue(id.Value, out var brand);
        return Task.FromResult(brand);
    }

    public Task<Brand?> GetByCodeAsync(BrandCode code, CancellationToken cancellationToken)
    {
        var found = _brands.Values.FirstOrDefault(b => b.Code.Value == code.Value);
        return Task.FromResult(found);
    }

    public Task<Brand?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        var found = _brands.Values.FirstOrDefault(b => b.NormalizedName == normalizedName);
        return Task.FromResult(found);
    }

    public Task<bool> ExistsCodeAsync(BrandCode code, CancellationToken cancellationToken)
    {
        var exists = _brands.Values.Any(b => b.Code.Value == code.Value);
        return Task.FromResult(exists);
    }

    public Task UpdateAsync(Brand brand, CancellationToken cancellationToken)
    {
        _brands[brand.Id.Value] = brand;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(BrandId id, CancellationToken cancellationToken)
    {
        _brands.Remove(id.Value);
        return Task.CompletedTask;
    }
}
