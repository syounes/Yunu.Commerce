namespace Yunu.Commerce.Catalog.Domain.Attributes;

/// <summary>
/// A single attribute assignment belonging to one Sku (docs task: "SKU
/// attribute foundation"). Not an Aggregate Root: it only exists inside the
/// Sku Aggregate's consistency boundary and is never persisted or referenced
/// independently. AttributeDefinitionId/AttributeOptionId identities are
/// resolved and validated by Catalog.Application against SQL Server
/// (Catalog.AttributeDefinitions / Catalog.AttributeOptions) before this type
/// is constructed; this type only protects the invariants that do not depend
/// on external reference data.
/// </summary>
public sealed class SkuAttribute
{
    public AttributeDefinitionId AttributeDefinitionId { get; }

    public string AttributeCode { get; }

    public int Sequence { get; }

    public SkuAttributeDataType DataType { get; }

    public SkuAttributeValue Value { get; }

    public AttributeOptionId? AttributeOptionId { get; }

    public SkuAttributeSource Source { get; }

    public decimal? Confidence { get; }

    private SkuAttribute(
        AttributeDefinitionId attributeDefinitionId,
        string attributeCode,
        int sequence,
        SkuAttributeDataType dataType,
        SkuAttributeValue value,
        AttributeOptionId? attributeOptionId,
        SkuAttributeSource source,
        decimal? confidence)
    {
        AttributeDefinitionId = attributeDefinitionId;
        AttributeCode = attributeCode;
        Sequence = sequence;
        DataType = dataType;
        Value = value;
        AttributeOptionId = attributeOptionId;
        Source = source;
        Confidence = confidence;
    }

    /// <summary>
    /// Creates a validated attribute assignment.
    ///
    /// Required invariants (docs task: "SKU attribute foundation"):
    /// AttributeCode cannot be null/empty/whitespace; Sequence must be greater
    /// than zero; Confidence, when provided, must be between 0 and 1; an Enum
    /// attribute must reference a valid AttributeOptionId (already resolved by
    /// Application - only presence is checked here); a non-Enum attribute must
    /// not carry an AttributeOptionId; the value's DataType must match the
    /// attribute's declared DataType.
    /// </summary>
    public static SkuAttribute Create(
        AttributeDefinitionId attributeDefinitionId,
        string attributeCode,
        int sequence,
        SkuAttributeValue value,
        AttributeOptionId? attributeOptionId = null,
        SkuAttributeSource source = SkuAttributeSource.User,
        decimal? confidence = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(attributeCode))
        {
            throw new ArgumentException("Attribute code cannot be null, empty or whitespace.", nameof(attributeCode));
        }

        if (sequence <= 0)
        {
            throw new ArgumentException("Attribute sequence must be greater than zero.", nameof(sequence));
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentException("Attribute confidence, when provided, must be between 0 and 1.", nameof(confidence));
        }

        if (value.DataType == SkuAttributeDataType.Enum && attributeOptionId is null)
        {
            throw new ArgumentException("An Enum attribute must reference a valid AttributeOptionId.", nameof(attributeOptionId));
        }

        if (value.DataType != SkuAttributeDataType.Enum && attributeOptionId is not null)
        {
            throw new ArgumentException("A non-Enum attribute must not reference an AttributeOptionId.", nameof(attributeOptionId));
        }

        return new SkuAttribute(
            attributeDefinitionId,
            attributeCode.Trim(),
            sequence,
            value.DataType,
            value,
            attributeOptionId,
            source,
            confidence);
    }

    /// <summary>
    /// Whether this assignment's effective value is identical to the supplied
    /// one, used by the Sku Aggregate to make repeated assignment idempotent
    /// (docs task: "assigning the same effective value again must be
    /// idempotent"). Source and Confidence do not affect effective-value
    /// equality.
    /// </summary>
    public bool HasSameEffectiveValueAs(SkuAttributeValue value, AttributeOptionId? attributeOptionId)
    {
        return Value == value && AttributeOptionId == attributeOptionId;
    }
}
