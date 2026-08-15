using System.Globalization;

namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Validates and normalizes a free-text rawValue according to an Attribute
/// Definition's DataType (docs task: "Semantic attribute hint resolution",
/// Etapa E). Never invents an AttributeOptionId/OptionCode: this is only used
/// for non-Enum attributes. Conversion failures are reported, never silently
/// coerced or truncated.
/// </summary>
internal static class AttributeValueValidator
{
    /// <summary>
    /// Attempts to normalize <paramref name="rawValue"/> according to
    /// <paramref name="dataType"/>. Returns null (and sets
    /// <paramref name="normalizedValue"/> to null) when the value cannot be
    /// safely interpreted; callers must treat this as
    /// <see cref="AttributeResolutionStatus.InvalidValue"/>.
    /// </summary>
    public static bool TryNormalize(string dataType, string? rawValue, out string? normalizedValue)
    {
        normalizedValue = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            // Absence of value is not itself invalid; callers decide whether
            // a value was required for this hint.
            return true;
        }

        var trimmed = rawValue.Trim();

        switch (dataType)
        {
            case "Text":
            case "Url":
            case "Json":
                // Free text/URL/JSON: preserved verbatim. Structural
                // validation (e.g. ISJSON) is deferred to persistence, which
                // does not happen in this resolution-only step.
                normalizedValue = trimmed;
                return true;

            case "Integer":
                if (long.TryParse(ExtractLeadingNumber(trimmed), NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
                {
                    normalizedValue = integerValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                return false;

            case "Decimal":
            case "Measurement":
            case "Money":
                if (decimal.TryParse(
                        ExtractLeadingNumber(trimmed).Replace(',', '.'),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var decimalValue))
                {
                    normalizedValue = decimalValue.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                return false;

            case "Boolean":
                var lowered = trimmed.ToLowerInvariant();

                if (lowered is "true" or "sim" or "verdadeiro" or "1")
                {
                    normalizedValue = "true";
                    return true;
                }

                if (lowered is "false" or "não" or "nao" or "falso" or "0")
                {
                    normalizedValue = "false";
                    return true;
                }

                return false;

            case "DateTime":
                if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTimeValue))
                {
                    normalizedValue = dateTimeValue.ToString("O", CultureInfo.InvariantCulture);
                    return true;
                }

                return false;

            default:
                // Unknown/unsupported DataType: do not silently accept.
                normalizedValue = null;
                return false;
        }
    }

    /// <summary>
    /// Extracts the leading numeric portion of a value such as "2 kg" or
    /// "41" without discarding the unit (the unit is not needed for
    /// resolution-only validation, but the original rawValue is preserved
    /// unchanged elsewhere).
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
