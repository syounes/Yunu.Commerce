namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Typed, deterministic result of parsing a free-text rawValue according to
/// an Attribute Definition's DataType (docs task: "Semantic attribute hint
/// resolution" - typed attribute value preservation). Mirrors the shape of
/// Catalog.SkuAttributeValues (deploy/sql/002_create_sku_attribute_catalog.sql)
/// so a future mapper to CatalogProductProposal/SkuAttributeValues can consume
/// this directly without reparsing RawValue/NormalizedValue.
///
/// Only the field(s) matching the attribute's DataType are ever populated;
/// callers (see <see cref="AttributeValueValidator"/>) are responsible for
/// this exclusivity rule. For Enum attributes this type is not used: the
/// official identity remains AttributeOptionId/OptionCode/OptionName on
/// <see cref="ResolvedAttributeHint"/>.
/// </summary>
public sealed record ResolvedAttributeValue(
    string DisplayValue,
    string? TextValue = null,
    long? IntegerValue = null,
    decimal? DecimalValue = null,
    bool? BooleanValue = null,
    DateTimeOffset? DateTimeValue = null,
    decimal? MoneyAmount = null,
    string? CurrencyCode = null,
    decimal? MeasurementValue = null,
    string? UnitCode = null,
    string? JsonValue = null);
