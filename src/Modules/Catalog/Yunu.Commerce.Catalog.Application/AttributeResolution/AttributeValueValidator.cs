using System.Globalization;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Validates and normalizes a free-text rawValue according to an Attribute
/// Definition's DataType (docs task: "Semantic attribute hint resolution",
/// Etapa E + typed attribute value preservation). Never invents an
/// AttributeOptionId/OptionCode: this is only used for non-Enum attributes.
/// Conversion failures are reported, never silently coerced or truncated.
/// Never uses an LLM for numeric/unit/currency parsing.
/// </summary>
internal static class AttributeValueValidator
{
    /// <summary>
    /// Attempts to parse <paramref name="rawValue"/> according to
    /// <paramref name="definition"/>'s DataType (and, for Measurement,
    /// UnitFamily). Returns null (and sets <paramref name="typedValue"/> to
    /// null, populating <paramref name="reason"/>) when the value cannot be
    /// safely interpreted; callers must treat this as
    /// <see cref="AttributeResolutionStatus.InvalidValue"/>.
    /// </summary>
    public static bool TryNormalize(
        AttributeDefinitionCatalogEntry definition,
        string? rawValue,
        out ResolvedAttributeValue? typedValue,
        out string? reason)
    {
        typedValue = null;
        reason = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            // Absence of value is not itself invalid; callers decide whether
            // a value was required for this hint.
            return true;
        }

        var trimmed = rawValue.Trim();

        switch (definition.DataType)
        {
            case "Text":
                typedValue = new ResolvedAttributeValue(trimmed, TextValue: trimmed);
                return true;

            case "Url":
                if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    reason = $"rawValue '{rawValue}' is not a valid absolute http(s) Url.";
                    return false;
                }

                typedValue = new ResolvedAttributeValue(trimmed, TextValue: trimmed);
                return true;

            case "Json":
                if (!IsValidJson(trimmed))
                {
                    reason = $"rawValue '{rawValue}' is not valid JSON.";
                    return false;
                }

                typedValue = new ResolvedAttributeValue(trimmed, JsonValue: trimmed);
                return true;

            case "Integer":
            {
                var numericText = ExtractLeadingNumber(trimmed).Replace(',', '.');

                if (numericText.Contains('.', StringComparison.Ordinal))
                {
                    reason = $"rawValue '{rawValue}' has a fractional part and cannot be interpreted as Integer.";
                    return false;
                }

                if (!long.TryParse(numericText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
                {
                    reason = $"rawValue '{rawValue}' could not be interpreted as Integer.";
                    return false;
                }

                if (!IsWithinNumericBounds(integerValue, definition, out reason))
                {
                    return false;
                }

                typedValue = new ResolvedAttributeValue(integerValue.ToString(CultureInfo.InvariantCulture), IntegerValue: integerValue);
                return true;
            }

            case "Decimal":
            {
                var numericText = ExtractLeadingNumber(trimmed).Replace(',', '.');

                if (!decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    reason = $"rawValue '{rawValue}' could not be interpreted as Decimal.";
                    return false;
                }

                if (!IsWithinNumericBounds(decimalValue, definition, out reason))
                {
                    return false;
                }

                typedValue = new ResolvedAttributeValue(decimalValue.ToString(CultureInfo.InvariantCulture), DecimalValue: decimalValue);
                return true;
            }

            case "Money":
                return TryParseMoney(rawValue, trimmed, definition, out typedValue, out reason);

            case "Measurement":
                return TryParseMeasurement(rawValue, trimmed, definition, out typedValue, out reason);

            case "Boolean":
            {
                var lowered = trimmed.ToLowerInvariant();

                if (lowered is "true" or "sim" or "verdadeiro" or "1")
                {
                    typedValue = new ResolvedAttributeValue("true", BooleanValue: true);
                    return true;
                }

                if (lowered is "false" or "não" or "nao" or "falso" or "0")
                {
                    typedValue = new ResolvedAttributeValue("false", BooleanValue: false);
                    return true;
                }

                reason = $"rawValue '{rawValue}' could not be interpreted as Boolean.";
                return false;
            }

            case "DateTime":
                if (!DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTimeValue))
                {
                    reason = $"rawValue '{rawValue}' could not be interpreted as DateTime.";
                    return false;
                }

                typedValue = new ResolvedAttributeValue(
                    dateTimeValue.ToString("O", CultureInfo.InvariantCulture),
                    DateTimeValue: dateTimeValue);
                return true;

            default:
                // Unknown/unsupported DataType: do not silently accept.
                reason = $"DataType '{definition.DataType}' is not supported.";
                return false;
        }
    }

    private static bool TryParseMoney(
        string rawValue,
        string trimmed,
        AttributeDefinitionCatalogEntry definition,
        out ResolvedAttributeValue? typedValue,
        out string? reason)
    {
        typedValue = null;
        reason = null;

        if (!TryExtractCurrencyCode(trimmed, out var currencyCode, out var remainder))
        {
            reason = $"rawValue '{rawValue}' does not contain a recognizable ISO 4217 currency code.";
            return false;
        }

        var numericText = ExtractLeadingNumber(remainder.Trim()).Replace(',', '.');

        if (!decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            reason = $"rawValue '{rawValue}' could not be interpreted as a monetary amount.";
            return false;
        }

        if (!IsWithinNumericBounds(amount, definition, out reason))
        {
            return false;
        }

        typedValue = new ResolvedAttributeValue($"{amount} {currencyCode}", MoneyAmount: amount, CurrencyCode: currencyCode);
        return true;
    }

    /// <summary>
    /// Recognizes a small set of common currency representations
    /// deterministically: the "R$"/"US$" symbol prefixes and explicit
    /// 3-letter ISO codes (as a prefix or suffix token). Never infers a
    /// currency from locale alone (docs task restriction §10).
    /// </summary>
    private static bool TryExtractCurrencyCode(string trimmed, out string currencyCode, out string remainder)
    {
        currencyCode = string.Empty;
        remainder = trimmed;

        if (trimmed.StartsWith("R$", StringComparison.OrdinalIgnoreCase))
        {
            currencyCode = "BRL";
            remainder = trimmed[2..];
            return true;
        }

        if (trimmed.StartsWith("US$", StringComparison.OrdinalIgnoreCase))
        {
            currencyCode = "USD";
            remainder = trimmed[3..];
            return true;
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length >= 2)
        {
            var first = tokens[0];
            var last = tokens[^1];

            if (IsIsoCurrencyCode(first))
            {
                currencyCode = first.ToUpperInvariant();
                remainder = string.Join(' ', tokens[1..]);
                return true;
            }

            if (IsIsoCurrencyCode(last))
            {
                currencyCode = last.ToUpperInvariant();
                remainder = string.Join(' ', tokens[..^1]);
                return true;
            }
        }

        return false;
    }

    private static bool IsIsoCurrencyCode(string token) =>
        token.Length == 3 && token.All(char.IsLetter);

    private static bool TryParseMeasurement(
        string rawValue,
        string trimmed,
        AttributeDefinitionCatalogEntry definition,
        out ResolvedAttributeValue? typedValue,
        out string? reason)
    {
        typedValue = null;
        reason = null;

        var numericText = ExtractLeadingNumber(trimmed);
        var unitText = trimmed[numericText.Length..].Trim();

        var normalizedNumericText = numericText.Replace(',', '.');

        if (string.IsNullOrEmpty(normalizedNumericText) ||
            !decimal.TryParse(normalizedNumericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var measurementValue))
        {
            reason = $"rawValue '{rawValue}' could not be interpreted as a Measurement (invalid numeric value).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(unitText))
        {
            reason = $"A unit is required for measurement attribute '{definition.Code}'.";
            return false;
        }

        if (!MeasurementUnitCatalog.TryResolve(unitText, out var canonicalUnitCode, out var unitFamily))
        {
            reason = $"Unit '{unitText}' is not a recognized measurement unit.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(definition.UnitFamily) &&
            !string.Equals(definition.UnitFamily, unitFamily, StringComparison.Ordinal))
        {
            reason = $"Unit '{canonicalUnitCode}' is incompatible with UnitFamily '{definition.UnitFamily}'.";
            return false;
        }

        if (!IsWithinNumericBounds(measurementValue, definition, out reason))
        {
            return false;
        }

        typedValue = new ResolvedAttributeValue(
            $"{measurementValue} {canonicalUnitCode}",
            MeasurementValue: measurementValue,
            UnitCode: canonicalUnitCode);
        return true;
    }

    private static bool IsWithinNumericBounds(decimal value, AttributeDefinitionCatalogEntry definition, out string? reason)
    {
        reason = null;

        if (definition.MinNumericValue is { } min && value < min)
        {
            reason = $"Value '{value}' is below the minimum allowed ({min}) for attribute '{definition.Code}'.";
            return false;
        }

        if (definition.MaxNumericValue is { } max && value > max)
        {
            reason = $"Value '{value}' is above the maximum allowed ({max}) for attribute '{definition.Code}'.";
            return false;
        }

        return true;
    }

    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the leading numeric portion of a value such as "2 kg" or
    /// "41", preserving the remainder (e.g. the unit) for callers that need
    /// it (Measurement).
    /// </summary>
    private static string ExtractLeadingNumber(string value)
    {
        var index = 0;

        while (index < value.Length && (char.IsDigit(value[index]) || value[index] is '.' or ',' or '-'))
        {
            index++;
        }

        return index == 0 ? value : value[..index];
    }
}
