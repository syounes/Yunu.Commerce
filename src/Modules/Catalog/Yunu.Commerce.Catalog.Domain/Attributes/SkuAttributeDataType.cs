namespace Yunu.Commerce.Catalog.Domain.Attributes;

/// <summary>
/// Data types supported by the Catalog attribute catalog, mirroring the
/// CHECK constraint on SQL Server Catalog.AttributeDefinitions.DataType
/// (deploy/sql/002_create_sku_attribute_catalog.sql). Sku attribute values
/// must always be constructed through <see cref="SkuAttributeValue"/>,
/// which guarantees the stored value matches its declared DataType.
/// </summary>
public enum SkuAttributeDataType
{
    Text,
    Integer,
    Decimal,
    Boolean,
    DateTime,
    Money,
    Measurement,
    Url,
    Enum,
    Json
}
