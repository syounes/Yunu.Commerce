using System.Text.Json;

namespace Yunu.Commerce.Catalog.Domain.Attributes;

/// <summary>
/// Value Object guaranteeing that a Sku attribute's stored value matches its
/// declared <see cref="SkuAttributeDataType"/>. Modeled as one sealed record
/// with private, type-specific static factories rather than either an
/// unvalidated dictionary or ten separate leaf types: the SQL Server
/// reference schema (Catalog.SkuAttributeValues,
/// deploy/sql/002_create_sku_attribute_catalog.sql) already stores one row
/// per assignment with per-type nullable columns, so mirroring that shape
/// here — while enforcing "only the matching typed field is populated" through
/// the factories — is the least complex option consistent with the existing
/// project conventions (docs task: "SKU attribute foundation").
///
/// Only one of the typed value groups below is ever populated for a given
/// instance; which one is determined exclusively by <see cref="DataType"/>
/// and enforced by the private constructor via the static factory used to
/// build it.
/// </summary>
public sealed record SkuAttributeValue
{
    public SkuAttributeDataType DataType { get; }

    public string? Text { get; }

    public long? Integer { get; }

    public decimal? Decimal { get; }

    public bool? Boolean { get; }

    public DateTime? DateTimeValue { get; }

    public decimal? MoneyAmount { get; }

    public string? CurrencyCode { get; }

    public decimal? MeasurementValue { get; }

    public string? UnitCode { get; }

    public string? Url { get; }

    public string? EnumOptionCode { get; }

    public string? Json { get; }

    /// <summary>
    /// Optional raw value as originally supplied by the caller, preserved for
    /// traceability. May differ from the typed/normalized representation
    /// (e.g. the original free-text string before numeric parsing).
    /// </summary>
    public string? RawValue { get; }

    /// <summary>
    /// Normalized textual representation of the value, used for display and
    /// search. Always populated.
    /// </summary>
    public string NormalizedValue { get; }

    private SkuAttributeValue(
        SkuAttributeDataType dataType,
        string normalizedValue,
        string? rawValue,
        string? text = null,
        long? integer = null,
        decimal? @decimal = null,
        bool? boolean = null,
        DateTime? dateTimeValue = null,
        decimal? moneyAmount = null,
        string? currencyCode = null,
        decimal? measurementValue = null,
        string? unitCode = null,
        string? url = null,
        string? enumOptionCode = null,
        string? json = null)
    {
        DataType = dataType;
        NormalizedValue = normalizedValue;
        RawValue = rawValue;
        Text = text;
        Integer = integer;
        Decimal = @decimal;
        Boolean = boolean;
        DateTimeValue = dateTimeValue;
        MoneyAmount = moneyAmount;
        CurrencyCode = currencyCode;
        MeasurementValue = measurementValue;
        UnitCode = unitCode;
        Url = url;
        EnumOptionCode = enumOptionCode;
        Json = json;
    }

    public static SkuAttributeValue ForText(string value, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Text attribute value cannot be null, empty or whitespace.", nameof(value));
        }

        var trimmed = value.Trim();

        return new SkuAttributeValue(SkuAttributeDataType.Text, trimmed, rawValue ?? value, text: trimmed);
    }

    public static SkuAttributeValue ForInteger(long value, string? rawValue = null)
    {
        return new SkuAttributeValue(SkuAttributeDataType.Integer, value.ToString(), rawValue, integer: value);
    }

    public static SkuAttributeValue ForDecimal(decimal value, string? rawValue = null)
    {
        return new SkuAttributeValue(SkuAttributeDataType.Decimal, value.ToString(), rawValue, @decimal: value);
    }

    public static SkuAttributeValue ForBoolean(bool value, string? rawValue = null)
    {
        return new SkuAttributeValue(SkuAttributeDataType.Boolean, value.ToString(), rawValue, boolean: value);
    }

    public static SkuAttributeValue ForDateTime(DateTime value, string? rawValue = null)
    {
        return new SkuAttributeValue(SkuAttributeDataType.DateTime, value.ToString("O"), rawValue, dateTimeValue: value);
    }

    /// <summary>
    /// A Money value must contain amount and ISO currency code
    /// (docs task: "SKU attribute foundation" - required invariants).
    /// </summary>
    public static SkuAttributeValue ForMoney(decimal amount, string currencyCode, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
        {
            throw new ArgumentException("Money currency code must be a 3-letter ISO 4217 code.", nameof(currencyCode));
        }

        var normalizedCurrency = currencyCode.Trim().ToUpperInvariant();

        return new SkuAttributeValue(
            SkuAttributeDataType.Money,
            $"{amount} {normalizedCurrency}",
            rawValue,
            moneyAmount: amount,
            currencyCode: normalizedCurrency);
    }

    /// <summary>
    /// A Measurement value must contain numeric value and unit code
    /// (docs task: "SKU attribute foundation" - required invariants).
    /// </summary>
    public static SkuAttributeValue ForMeasurement(decimal value, string unitCode, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            throw new ArgumentException("Measurement unit code cannot be null, empty or whitespace.", nameof(unitCode));
        }

        var normalizedUnit = unitCode.Trim();

        return new SkuAttributeValue(
            SkuAttributeDataType.Measurement,
            $"{value} {normalizedUnit}",
            rawValue,
            measurementValue: value,
            unitCode: normalizedUnit);
    }

    public static SkuAttributeValue ForUrl(string value, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Url attribute value cannot be null, empty or whitespace.", nameof(value));
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out _))
        {
            throw new ArgumentException($"'{value}' is not a valid absolute Url.", nameof(value));
        }

        var trimmed = value.Trim();

        return new SkuAttributeValue(SkuAttributeDataType.Url, trimmed, rawValue ?? value, url: trimmed);
    }

    /// <summary>
    /// An Enum attribute must reference a valid AttributeOptionId resolved by
    /// Application (enforced by <see cref="SkuAttribute"/>, not here); this
    /// factory only guarantees the option code text itself is present.
    /// </summary>
    public static SkuAttributeValue ForEnum(string optionCode, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(optionCode))
        {
            throw new ArgumentException("Enum attribute option code cannot be null, empty or whitespace.", nameof(optionCode));
        }

        var trimmed = optionCode.Trim();

        return new SkuAttributeValue(SkuAttributeDataType.Enum, trimmed, rawValue ?? optionCode, enumOptionCode: trimmed);
    }

    public static SkuAttributeValue ForJson(string json, string? rawValue = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Json attribute value cannot be null, empty or whitespace.", nameof(json));
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"'{json}' is not valid JSON.", nameof(json), ex);
        }

        return new SkuAttributeValue(SkuAttributeDataType.Json, json.Trim(), rawValue ?? json, json: json.Trim());
    }
}
