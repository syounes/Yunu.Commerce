using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.SourceTaxonomy.Google;

/// <summary>
/// Anti-Corruption Layer adapter (docs/adr/0014-provider-neutral-source-taxonomy.md
/// §9-§10) translating the existing persisted Google Product Taxonomy
/// (<c>Catalog.GoogleTaxonomyCategories</c>) into a provider-neutral
/// <see cref="SourceTaxonomySnapshot"/>, consumed by the existing generic
/// <see cref="SourceTaxonomyImportOrchestrator"/>.
///
/// This adapter is read-only toward the Google native model: it never
/// writes to <c>Catalog.GoogleTaxonomyCategories</c> and never calls
/// <c>IGoogleTaxonomyRepository.GetActiveAsync</c>, which would silently
/// drop inactive rows. It reads the complete dataset directly via ADO.NET,
/// matching the existing Infrastructure convention for this bounded
/// context (<see cref="SqlGoogleTaxonomyRepository"/>).
///
/// Provider-specific knowledge (Google identifiers, Google SQL shape) ends
/// here; the generic SourceTaxonomy core contains zero Google-specific
/// branching.
/// </summary>
public sealed class GoogleSourceTaxonomyAdapter : ISourceTaxonomyAdapter
{
    public const string GoogleAdapterCode = "google-product-taxonomy";
    public const string GoogleProviderCode = "google";

    private readonly string _connectionString;

    public GoogleSourceTaxonomyAdapter(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public string AdapterCode => GoogleAdapterCode;

    public async Task<SourceTaxonomySnapshot> LoadAsync(
        SourceTaxonomyImportContext context,
        CancellationToken cancellationToken)
    {
        var rows = await LoadGoogleCategoriesAsync(cancellationToken);

        var locale = ResolveConsistentLocale(rows);

        if (!IsLocaleCompatible(context.DefaultLanguage, locale))
        {
            throw new GoogleSourceTaxonomyLanguageMismatchException(context.DefaultLanguage, locale);
        }

        var nodes = rows
            .Select(row => new SourceTaxonomySnapshotNode
            {
                ExternalNodeId = row.GoogleCategoryId.ToString(CultureInfo.InvariantCulture),
                ParentExternalNodeId = row.ParentGoogleCategoryId?.ToString(CultureInfo.InvariantCulture),
                NodeType = "Category",
                Name = row.Name,
                FullPath = row.FullPath,
                Level = row.Level,
                IsLeaf = row.IsLeaf,
                IsActive = row.IsActive
            })
            .ToArray();

        var descriptor = new SourceTaxonomySnapshotDescriptor
        {
            ProviderCode = GoogleProviderCode,
            ScopeCode = null,
            ExternalTaxonomyId = null,
            ExternalVersion = null,
            Locale = locale,
            SourceUri = context.SourceUri,
            SourceChecksum = null
        };

        return new SourceTaxonomySnapshot
        {
            Descriptor = descriptor,
            Nodes = nodes
        };
    }

    private static string ResolveConsistentLocale(IReadOnlyCollection<GoogleTaxonomyCategoryRow> rows)
    {
        string? locale = null;

        foreach (var row in rows)
        {
            if (locale is null)
            {
                locale = row.SourceLanguage;
                continue;
            }

            if (!string.Equals(locale, row.SourceLanguage, StringComparison.Ordinal))
            {
                throw new GoogleSourceTaxonomyInconsistentLanguageException(locale, row.SourceLanguage);
            }
        }

        return locale
            ?? throw new SourceTaxonomySnapshotValidationException("The persisted Google taxonomy dataset is empty; no SourceLanguage could be determined.");
    }

    /// <summary>
    /// Deterministic locale compatibility rule for SourceTaxonomy identity
    /// (docs task: "Final Audit Corrections Before PR" §4). Two locales are
    /// compatible only when:
    /// (A) they are exactly equal, ignoring case; or
    /// (B) exactly one side is a primary-language-only value (no region
    /// subtag) and its primary language matches the other side's primary
    /// language.
    /// When BOTH sides carry an explicit region subtag, they must match
    /// exactly; "en-US" and "en-GB" (or "pt-BR" and "pt-PT") are NOT
    /// compatible. This intentionally does not implement a general
    /// localization/negotiation framework.
    /// </summary>
    private static bool IsLocaleCompatible(string defaultLanguage, string locale)
    {
        if (string.Equals(defaultLanguage, locale, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var defaultHasRegion = HasRegionSubtag(defaultLanguage);
        var localeHasRegion = HasRegionSubtag(locale);

        if (defaultHasRegion && localeHasRegion)
        {
            // Both are specific locales but did not match exactly above.
            return false;
        }

        var defaultPrimary = PrimarySubtag(defaultLanguage);
        var localePrimary = PrimarySubtag(locale);

        return string.Equals(defaultPrimary, localePrimary, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRegionSubtag(string language) => language.Contains('-');

    private static string PrimarySubtag(string language)
    {
        var separatorIndex = language.IndexOf('-');
        return separatorIndex < 0 ? language : language[..separatorIndex];
    }

    private async Task<IReadOnlyCollection<GoogleTaxonomyCategoryRow>> LoadGoogleCategoriesAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage
            FROM [Catalog].[GoogleTaxonomyCategories]
            ORDER BY GoogleCategoryId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<GoogleTaxonomyCategoryRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GoogleTaxonomyCategoryRow(
                GoogleCategoryId: reader.GetInt32(0),
                ParentGoogleCategoryId: reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Name: reader.GetString(2),
                FullPath: reader.GetString(3),
                Level: reader.GetInt32(4),
                IsLeaf: reader.GetBoolean(5),
                IsActive: reader.GetBoolean(6),
                SourceLanguage: reader.GetString(7)));
        }

        return results;
    }

    /// <summary>
    /// Tiny provider-specific read model used only inside this adapter,
    /// distinct from <c>GoogleTaxonomyCategoryResponse</c> because it must
    /// also expose <c>SourceLanguage</c> and every row (including inactive
    /// ones), which the existing query read model intentionally omits.
    /// </summary>
    private sealed record GoogleTaxonomyCategoryRow(
        int GoogleCategoryId,
        int? ParentGoogleCategoryId,
        string Name,
        string FullPath,
        int Level,
        bool IsLeaf,
        bool IsActive,
        string SourceLanguage);
}
