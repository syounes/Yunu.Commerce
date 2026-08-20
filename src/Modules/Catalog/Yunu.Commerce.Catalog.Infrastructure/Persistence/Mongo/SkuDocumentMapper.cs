using Yunu.Commerce.Catalog.Domain.Attributes;
using Yunu.Commerce.Catalog.Domain.Products;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// Explicit, hand-written mapping between the Sku Aggregate and its MongoDB
/// persistence document (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
/// No AutoMapper is used (docs/adr/0001 §9, "prefer explicit mapping"). Never
/// reads or writes Sku.DomainEvents.
///
/// Attribute mapping (docs task: "SKU attribute foundation") round-trips every
/// supported <see cref="SkuAttributeDataType"/> through the matching typed
/// field on <see cref="SkuAttributeDocument"/>. A missing/null "Attributes"
/// field on legacy documents hydrates as an empty collection.
/// </summary>
internal static class SkuDocumentMapper
{
    public static SkuDocument ToDocument(Sku sku)
    {
        return new SkuDocument
        {
            Id = sku.Id.Value,
            ProductId = sku.ProductId.Value,
            Code = sku.Code.Value,
            Gtin = sku.Gtin,
            Status = sku.Status.ToString(),
            Attributes = sku.Attributes.Select(ToAttributeDocument).ToList(),
            SegmentAssignments = sku.SegmentAssignments.Select(sa => new SkuSegmentAssignmentDocument
            {
                SegmentDefinitionId = sa.SegmentDefinitionId.Value,
                SegmentCode = sa.SegmentCode,
                Options = sa.Options.Select(o => new SkuSegmentOptionSelectionDocument
                {
                    SegmentOptionId = o.SegmentOptionId.Value,
                    OptionCode = o.OptionCode
                }).ToList()
            }).ToList()
        };
    }

    public static Sku ToDomain(SkuDocument document)
    {
        var attributes = (document.Attributes ?? new List<SkuAttributeDocument>())
            .Select(ToDomainAttribute);

        var segmentAssignments = (document.SegmentAssignments ?? new List<SkuSegmentAssignmentDocument>())
            .Select(sa => SegmentAssignment.Hydrate(
                new SegmentDefinitionId(sa.SegmentDefinitionId),
                sa.SegmentCode,
                sa.Options.Select(o => new SegmentOptionSelection(new SegmentOptionId(o.SegmentOptionId), o.OptionCode))));

        return Sku.Hydrate(
            new SkuId(document.Id),
            new ProductId(document.ProductId),
            new SkuCode(document.Code),
            document.Gtin,
            Enum.Parse<SkuStatus>(document.Status),
            attributes,
            segmentAssignments);
    }

    private static SkuAttributeDocument ToAttributeDocument(SkuAttribute attribute)
    {
        var value = attribute.Value;

        return new SkuAttributeDocument
        {
            AttributeDefinitionId = attribute.AttributeDefinitionId.Value,
            AttributeCode = attribute.AttributeCode,
            Sequence = attribute.Sequence,
            DataType = attribute.DataType.ToString(),
            RawValue = value.RawValue,
            NormalizedValue = value.NormalizedValue,
            Text = value.Text,
            Integer = value.Integer,
            Decimal = value.Decimal,
            Boolean = value.Boolean,
            DateTimeValue = value.DateTimeValue,
            MoneyAmount = value.MoneyAmount,
            CurrencyCode = value.CurrencyCode,
            MeasurementValue = value.MeasurementValue,
            UnitCode = value.UnitCode,
            Url = value.Url,
            EnumOptionCode = value.EnumOptionCode,
            Json = value.Json,
            AttributeOptionId = attribute.AttributeOptionId?.Value,
            Source = attribute.Source.ToString(),
            Confidence = attribute.Confidence
        };
    }

    private static SkuAttribute ToDomainAttribute(SkuAttributeDocument document)
    {
        var dataType = Enum.Parse<SkuAttributeDataType>(document.DataType);

        var value = dataType switch
        {
            SkuAttributeDataType.Text => SkuAttributeValue.ForText(document.Text!, document.RawValue),
            SkuAttributeDataType.Integer => SkuAttributeValue.ForInteger(document.Integer!.Value, document.RawValue),
            SkuAttributeDataType.Decimal => SkuAttributeValue.ForDecimal(document.Decimal!.Value, document.RawValue),
            SkuAttributeDataType.Boolean => SkuAttributeValue.ForBoolean(document.Boolean!.Value, document.RawValue),
            SkuAttributeDataType.DateTime => SkuAttributeValue.ForDateTime(document.DateTimeValue!.Value, document.RawValue),
            SkuAttributeDataType.Money => SkuAttributeValue.ForMoney(document.MoneyAmount!.Value, document.CurrencyCode!, document.RawValue),
            SkuAttributeDataType.Measurement => SkuAttributeValue.ForMeasurement(document.MeasurementValue!.Value, document.UnitCode!, document.RawValue),
            SkuAttributeDataType.Url => SkuAttributeValue.ForUrl(document.Url!, document.RawValue),
            SkuAttributeDataType.Enum => SkuAttributeValue.ForEnum(document.EnumOptionCode!, document.RawValue),
            SkuAttributeDataType.Json => SkuAttributeValue.ForJson(document.Json!, document.RawValue),
            _ => throw new InvalidOperationException($"Unsupported attribute DataType '{document.DataType}'.")
        };

        var attributeOptionId = document.AttributeOptionId is { } optionId ? new AttributeOptionId(optionId) : (AttributeOptionId?)null;
        var source = Enum.Parse<SkuAttributeSource>(document.Source);

        return SkuAttribute.Create(
            new AttributeDefinitionId(document.AttributeDefinitionId),
            document.AttributeCode,
            document.Sequence,
            value,
            attributeOptionId,
            source,
            document.Confidence);
    }
}

