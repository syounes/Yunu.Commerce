namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Small, explicit and testable catalog of Measurement unit aliases and their
/// canonical UnitCode + UnitFamily (docs task: "Semantic attribute hint
/// resolution" - Measurement parsing). No LLM/semantic matching is used here:
/// resolution is a deterministic dictionary lookup, and no partial/Contains
/// matching is performed (to avoid confusing "m", "ml" and "mm").
///
/// Unit conversion (e.g. kg -> g) is explicitly out of scope: values are only
/// parsed, normalized and preserved as originally expressed (docs task
/// restriction §5).
/// </summary>
internal static class MeasurementUnitCatalog
{
    private sealed record UnitEntry(string CanonicalCode, string Family);

    // Keys are already-normalized (trimmed, lowercased, diacritics-stripped)
    // aliases; see AttributeHintNormalizer.Normalize. Exact lookup only.
    private static readonly Dictionary<string, UnitEntry> Aliases = new(StringComparer.Ordinal)
    {
        // Weight
        ["kg"] = new("kg", "Weight"),
        ["quilo"] = new("kg", "Weight"),
        ["quilos"] = new("kg", "Weight"),
        ["quilograma"] = new("kg", "Weight"),
        ["quilogramas"] = new("kg", "Weight"),
        ["g"] = new("g", "Weight"),
        ["grama"] = new("g", "Weight"),
        ["gramas"] = new("g", "Weight"),
        ["mg"] = new("mg", "Weight"),
        ["miligrama"] = new("mg", "Weight"),
        ["miligramas"] = new("mg", "Weight"),
        ["lb"] = new("lb", "Weight"),
        ["libra"] = new("lb", "Weight"),
        ["libras"] = new("lb", "Weight"),
        ["oz"] = new("oz", "Weight"),
        ["onca"] = new("oz", "Weight"),
        ["oncas"] = new("oz", "Weight"),

        // Length
        ["mm"] = new("mm", "Length"),
        ["milimetro"] = new("mm", "Length"),
        ["milimetros"] = new("mm", "Length"),
        ["cm"] = new("cm", "Length"),
        ["centimetro"] = new("cm", "Length"),
        ["centimetros"] = new("cm", "Length"),
        ["m"] = new("m", "Length"),
        ["metro"] = new("m", "Length"),
        ["metros"] = new("m", "Length"),

        // Volume
        ["ml"] = new("ml", "Volume"),
        ["mililitro"] = new("ml", "Volume"),
        ["mililitros"] = new("ml", "Volume"),
        ["l"] = new("l", "Volume"),
        ["litro"] = new("l", "Volume"),
        ["litros"] = new("l", "Volume"),
    };

    /// <summary>
    /// Attempts to resolve <paramref name="rawUnit"/> (already trimmed as
    /// typed by the caller) into its canonical UnitCode and UnitFamily.
    /// </summary>
    public static bool TryResolve(string rawUnit, out string canonicalUnitCode, out string family)
    {
        canonicalUnitCode = string.Empty;
        family = string.Empty;

        var normalized = AttributeHintNormalizer.Normalize(rawUnit);

        if (!Aliases.TryGetValue(normalized, out var entry))
        {
            return false;
        }

        canonicalUnitCode = entry.CanonicalCode;
        family = entry.Family;
        return true;
    }
}
