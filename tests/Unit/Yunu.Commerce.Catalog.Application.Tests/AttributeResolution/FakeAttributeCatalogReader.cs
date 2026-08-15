using Yunu.Commerce.Catalog.Application.AttributeResolution;

namespace Yunu.Commerce.Catalog.Application.Tests.AttributeResolution;

/// <summary>
/// Test-only fake for IAttributeCatalogReader. Backed by simple in-memory
/// collections seeded per test; never touches SQL Server.
/// </summary>
internal sealed class FakeAttributeCatalogReader : IAttributeCatalogReader
{
    private readonly List<AttributeDefinitionCatalogEntry> _definitions = [];
    private readonly List<AttributeOptionCatalogEntry> _options = [];
    private readonly List<GoogleCategoryAttributeRuleEntry> _rules = [];

    public void AddDefinition(AttributeDefinitionCatalogEntry definition) => _definitions.Add(definition);

    public void AddOption(AttributeOptionCatalogEntry option) => _options.Add(option);

    public void AddRule(GoogleCategoryAttributeRuleEntry rule) => _rules.Add(rule);

    public Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> FindDefinitionsByExactMatchAsync(
        IReadOnlyCollection<string> normalizedValues,
        CancellationToken cancellationToken)
    {
        var normalizedSet = new HashSet<string>(normalizedValues, StringComparer.Ordinal);

        var matches = _definitions
            .Where(d => d.IsActive && (
                normalizedSet.Contains(AttributeResolutionTestNormalizer.Normalize(d.Code)) ||
                normalizedSet.Contains(AttributeResolutionTestNormalizer.Normalize(d.Name)) ||
                (d.GoogleAttributeName is not null && normalizedSet.Contains(AttributeResolutionTestNormalizer.Normalize(d.GoogleAttributeName)))))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AttributeDefinitionCatalogEntry>>(matches);
    }

    public Task<IReadOnlyList<AttributeDefinitionCatalogEntry>> GetDefinitionsByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        var codeSet = new HashSet<string>(codes, StringComparer.Ordinal);

        var matches = _definitions
            .Where(d => d.IsActive && codeSet.Contains(d.Code))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AttributeDefinitionCatalogEntry>>(matches);
    }

    public Task<IReadOnlyList<AttributeOptionCatalogEntry>> FindOptionsByExactMatchAsync(
        int attributeDefinitionId,
        IReadOnlyCollection<string> normalizedValues,
        CancellationToken cancellationToken)
    {
        var normalizedSet = new HashSet<string>(normalizedValues, StringComparer.Ordinal);

        var matches = _options
            .Where(o => o.IsActive && o.AttributeDefinitionId == attributeDefinitionId && (
                normalizedSet.Contains(AttributeResolutionTestNormalizer.Normalize(o.Code)) ||
                normalizedSet.Contains(AttributeResolutionTestNormalizer.Normalize(o.Name)) ||
                (o.GoogleValue is not null && normalizedSet.Contains(AttributeResolutionTestNormalizer.Normalize(o.GoogleValue)))))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AttributeOptionCatalogEntry>>(matches);
    }

    public Task<IReadOnlyList<AttributeOptionCatalogEntry>> GetOptionsByCodesAsync(
        int attributeDefinitionId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        var codeSet = new HashSet<string>(codes, StringComparer.Ordinal);

        var matches = _options
            .Where(o => o.IsActive && o.AttributeDefinitionId == attributeDefinitionId && codeSet.Contains(o.Code))
            .ToArray();

        return Task.FromResult<IReadOnlyList<AttributeOptionCatalogEntry>>(matches);
    }

    public Task<IReadOnlyList<GoogleCategoryAttributeRuleEntry>> GetCategoryRulesAsync(
        long googleCategoryId,
        IReadOnlyCollection<int> attributeDefinitionIds,
        CancellationToken cancellationToken)
    {
        var idSet = new HashSet<int>(attributeDefinitionIds);

        var matches = _rules
            .Where(r => r.GoogleCategoryId == googleCategoryId && idSet.Contains(r.AttributeDefinitionId))
            .ToArray();

        return Task.FromResult<IReadOnlyList<GoogleCategoryAttributeRuleEntry>>(matches);
    }
}

/// <summary>
/// Minimal accent/case-insensitive normalizer duplicated for test fakes only,
/// mirroring the production AttributeHintNormalizer behavior without taking a
/// dependency on an internal type.
/// </summary>
internal static class AttributeResolutionTestNormalizer
{
    public static string Normalize(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var decomposed = trimmed.Normalize(System.Text.NormalizationForm.FormD);

        var builder = new System.Text.StringBuilder();

        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
