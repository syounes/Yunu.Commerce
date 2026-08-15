using Yunu.Commerce.Catalog.Domain.Attributes;

namespace Yunu.Commerce.Catalog.Application.Skus;

/// <summary>
/// Shared mapping between <see cref="SkuAttribute"/> (Domain) and
/// <see cref="SkuAttributeResponse"/> (Application read model), reused by
/// every Sku read model and by CreateSku's result (docs task: "SKU attribute
/// foundation").
/// </summary>
internal static class SkuAttributeResponseMapper
{
    public static SkuAttributeResponse ToResponse(SkuAttribute attribute)
    {
        return new SkuAttributeResponse
        {
            AttributeDefinitionId = attribute.AttributeDefinitionId.Value,
            AttributeCode = attribute.AttributeCode,
            Sequence = attribute.Sequence,
            DataType = attribute.DataType.ToString(),
            RawValue = attribute.Value.RawValue,
            NormalizedValue = attribute.Value.NormalizedValue,
            AttributeOptionId = attribute.AttributeOptionId?.Value,
            Source = attribute.Source.ToString(),
            Confidence = attribute.Confidence
        };
    }
}
