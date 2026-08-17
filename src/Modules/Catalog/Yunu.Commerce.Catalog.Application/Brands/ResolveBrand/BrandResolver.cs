using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.ResolveBrand;

/// <summary>
/// Deterministic Brand resolver (docs/domains/catalog.md §12 — no AI, no aliases,
/// no auto-creation). Resolution order: exact BrandCode, then exact NormalizedName.
/// </summary>
public sealed class BrandResolver : IBrandResolver
{
    private readonly IBrandRepository _repository;

    public BrandResolver(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Brand?> ResolveAsync(string input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // Try exact code first
        try
        {
            var code = new BrandCode(input);
            var byCode = await _repository.GetByCodeAsync(code, cancellationToken);
            if (byCode != null) return byCode;
        }
        catch (ArgumentException)
        {
            // not a valid code, continue to normalized name resolution
        }

        var normalized = Brand.ComputeNormalizedName(input);
        return await _repository.FindByNormalizedNameAsync(normalized, cancellationToken);
    }
}
