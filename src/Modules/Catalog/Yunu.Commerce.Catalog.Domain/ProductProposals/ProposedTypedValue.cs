namespace Yunu.Commerce.Catalog.Domain.ProductProposals;

/// <summary>
/// Typed, structured representation of a proposed attribute's value,
/// preserved verbatim from Application's attribute hint resolution result
/// (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). Mirrors the shape already used by attribute hint
/// resolution so measurements, money and other typed values are never
/// degraded back into plain strings. Only the field(s) matching the
/// attribute's DataType are ever populated.
/// </summary>
public sealed record ProposedTypedValue(
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
