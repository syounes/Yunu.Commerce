namespace Yunu.Commerce.Catalog.Application.AttributeResolution;

/// <summary>
/// Google Merchant category-level requirement for an attribute
/// (Catalog.GoogleCategoryAttributeRules.RequirementLevel,
/// deploy/sql/002_create_sku_attribute_catalog.sql). Only populated when a
/// GoogleCategoryId is supplied and a rule exists for it.
/// </summary>
public enum AttributeRequirementLevel
{
    Required,
    Recommended,
    Optional
}
