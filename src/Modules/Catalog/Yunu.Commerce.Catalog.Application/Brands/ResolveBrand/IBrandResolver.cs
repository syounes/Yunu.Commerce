using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.ResolveBrand;

public interface IBrandResolver
{
    /// <summary>
    /// Resolve by exact BrandCode first, then by NormalizedName. Returns null if not found.
    /// </summary>
    Task<Brand?> ResolveAsync(string input, CancellationToken cancellationToken);
}
