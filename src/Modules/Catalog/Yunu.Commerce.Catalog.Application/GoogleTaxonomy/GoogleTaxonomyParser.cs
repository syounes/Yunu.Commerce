using System.Globalization;

namespace Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

/// <summary>
/// Parses raw Google Product Taxonomy lines into structured, unvalidated rows.
/// Responsible only for text-level transformation: it does not resolve parent
/// relationships, does not compute IsLeaf, and does not touch persistence.
/// Malformed lines are safely rejected (skipped) rather than throwing, so a
/// single corrupt line does not abort parsing of the entire feed.
/// </summary>
public static class GoogleTaxonomyParser
{
    private const string Separator = " - ";
    private const string PathSeparator = " > ";

    /// <summary>
    /// Parses a single Google taxonomy line such as:
    /// "2271 - Apparel &amp; Accessories &gt; Clothing &gt; Dresses"
    /// into a <see cref="ParsedGoogleTaxonomyRow"/>, or returns null when the
    /// line is blank, a comment/header, or otherwise malformed.
    /// </summary>
    public static ParsedGoogleTaxonomyRow? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var trimmedLine = line.Trim();

        if (trimmedLine.StartsWith('#'))
        {
            return null;
        }

        var separatorIndex = trimmedLine.IndexOf(Separator, StringComparison.Ordinal);

        if (separatorIndex <= 0)
        {
            return null;
        }

        var idPart = trimmedLine[..separatorIndex].Trim();
        var pathPart = trimmedLine[(separatorIndex + Separator.Length)..].Trim();

        if (!int.TryParse(idPart, NumberStyles.None, CultureInfo.InvariantCulture, out var googleCategoryId)
            || googleCategoryId <= 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(pathPart))
        {
            return null;
        }

        var segments = pathPart
            .Split(PathSeparator, StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (segments.Length == 0)
        {
            return null;
        }

        var fullPath = string.Join(PathSeparator, segments);
        var name = segments[^1];
        var level = segments.Length - 1;

        return new ParsedGoogleTaxonomyRow(googleCategoryId, name, fullPath, level);
    }

    /// <summary>
    /// Parses every line of the raw Google taxonomy feed, silently skipping
    /// blank, comment/header or malformed lines.
    /// </summary>
    public static IReadOnlyCollection<ParsedGoogleTaxonomyRow> Parse(IReadOnlyCollection<string> lines)
    {
        var rows = new List<ParsedGoogleTaxonomyRow>();

        foreach (var line in lines)
        {
            var row = ParseLine(line);

            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }
}

/// <summary>
/// A single parsed Google taxonomy row before parent/child hierarchy resolution.
/// </summary>
public sealed record ParsedGoogleTaxonomyRow(
    int GoogleCategoryId,
    string Name,
    string FullPath,
    int Level);
