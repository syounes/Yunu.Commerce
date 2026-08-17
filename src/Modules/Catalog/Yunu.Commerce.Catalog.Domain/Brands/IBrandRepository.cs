namespace Yunu.Commerce.Catalog.Domain.Brands;

public interface IBrandRepository
{
    Task AddAsync(Brand brand, CancellationToken cancellationToken);

    Task UpdateAsync(Brand brand, CancellationToken cancellationToken);

    Task<Brand?> GetByIdAsync(BrandId id, CancellationToken cancellationToken);

    Task<Brand?> GetByCodeAsync(BrandCode code, CancellationToken cancellationToken);

    Task<Brand?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken);

    Task<bool> ExistsCodeAsync(BrandCode code, CancellationToken cancellationToken);
}
