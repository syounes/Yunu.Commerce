using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Catalog.Application.Brands.ResolveBrand;

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
        catch
        {
            // not a valid code, continue to normalized name resolution
        }

        var normalized = Normalize(input);
        return await _repository.FindByNormalizedNameAsync(normalized, cancellationToken);
    }

    private static string Normalize(string name)
    {
        var trimmed = name.Trim();
        var normalized = System.Text.RegularExpressions.Regex.Replace(RemoveDiacritics(trimmed), "\\s+", " ");
        return normalized.ToUpperInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
