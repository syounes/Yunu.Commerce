/*
 Yunu.Commerce - SKU attribute catalog: connection_type attribute (SQL Server)
 Adds a semantically dedicated Enum attribute definition for connection/
 interface type (e.g. USB, USB-C, Bluetooth, P2, P3, P10, XLR, HDMI, Ethernet,
 Wi-Fi), replacing the previous gap where "conexão = USB" had no adequate
 attribute definition and risked being mapped into the generic Json
 product_detail attribute.

 connection_type is an internal Yunu.Commerce catalog definition and is NOT an
 official standalone Google Merchant attribute. "connectivity" does not exist
 as an independent Google Merchant Center attribute; connectivity information
 may in the future be exported as a structured entry inside product_detail
 (e.g. Connectivity:Connectivity Technology:USB). That transformation is the
 responsibility of the Google Merchant feed/adapter, not of this migration.
 Therefore GoogleAttributeName is NULL and IsGoogleMerchantAttribute is 0.

 connection_type is primarily a technical specification attribute (Cardinality
 = Multiple, e.g. "USB-C + Bluetooth + Wi-Fi" for the same SKU) rather than a
 variant axis used to generate SKU combinations (unlike color/size/material in
 AttributeGroupId 3 = VARIANT, see 002_create_sku_attribute_catalog.sql). It is
 therefore placed in AttributeGroupId 6 = CLASSIFICATION, alongside other
 multi-value technical/classification attributes such as compatibility (65)
 and feature (68), with IsVariantAxis = 0.

 Idempotent migration: safe to execute more than once. Matching is done both
 by surrogate ID and by natural key (Code / AttributeDefinitionId+Code) to
 avoid accidentally updating an unrelated row that happens to reuse an ID and
 to avoid inserting duplicates if the natural key already exists under a
 different ID.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

BEGIN TRY

/* Guard: fail fast (and roll back) if the chosen surrogate IDs are already
   in use by a different natural key than the one this migration owns. This
   prevents silently corrupting an unrelated attribute/option. */
IF EXISTS (
    SELECT 1 FROM Catalog.AttributeDefinitions
    WHERE AttributeDefinitionId = 69 AND Code <> 'connection_type'
)
    THROW 51000, 'AttributeDefinitionId 69 is already assigned to a different Code.', 1;

IF EXISTS (
    SELECT 1 FROM Catalog.AttributeDefinitions
    WHERE Code = 'connection_type' AND AttributeDefinitionId <> 69
)
    THROW 51000, 'Code connection_type already exists under a different AttributeDefinitionId.', 1;

IF EXISTS (
    SELECT 1 FROM Catalog.AttributeOptions o
    JOIN (VALUES (1901,'USB'),(1902,'USB_C'),(1903,'BLUETOOTH'),(1904,'WIFI'),
                  (1905,'P2'),(1906,'P3'),(1907,'P10'),(1908,'XLR'),
                  (1909,'HDMI'),(1910,'ETHERNET')) v(Id,Code) ON o.AttributeOptionId = v.Id
    WHERE o.AttributeDefinitionId <> 69 OR o.Code <> v.Code
)
    THROW 51000, 'One or more AttributeOptionId values reserved for connection_type are already assigned to a different definition/code.', 1;

IF EXISTS (
    SELECT 1 FROM Catalog.AttributeOptions
    WHERE AttributeDefinitionId = 69
      AND Code IN ('USB','USB_C','BLUETOOTH','WIFI','P2','P3','P10','XLR','HDMI','ETHERNET')
      AND AttributeOptionId NOT IN (1901,1902,1903,1904,1905,1906,1907,1908,1909,1910)
)
    THROW 51000, 'A connection_type option code already exists under a different AttributeOptionId.', 1;

/* AttributeGroupId 6 = CLASSIFICATION (see 002_create_sku_attribute_catalog.sql).
   connection_type is a technical/classification specification, not a variant
   axis: IsVariantAxis = 0. It remains searchable and filterable, but is not
   required by default. */
MERGE Catalog.AttributeDefinitions AS t USING (VALUES
 (69,6,'connection_type',NULL,N'Tipo de conexão',
  N'Interface ou tipo de conexão física ou sem fio suportada pelo item. Aceita múltiplos valores simultâneos.',
  N'tipo de conexão conectividade tecnologia de conexão interface conector entrada saída porta conexão física conexão sem fio wireless com fio usb usb-c bluetooth wi-fi p2 p3 p10 xlr hdmi ethernet rj45',
  'Enum','Multiple',NULL,NULL,NULL,NULL,NULL,0,0,1,1,0,140)
) s(Id,GroupId,Code,GoogleName,Name,Description,SemanticText,DataType,Cardinality,UnitFamily,Regex,MinValue,MaxValue,MaxLength,IsGoogle,IsVariant,IsSearchable,IsFilterable,IsRequired,DisplayOrder)
ON t.AttributeDefinitionId = s.Id OR t.Code = s.Code
WHEN MATCHED THEN UPDATE SET
    AttributeDefinitionId=s.Id,AttributeGroupId=s.GroupId,Code=s.Code,GoogleAttributeName=s.GoogleName,
    Name=s.Name,Description=s.Description,SemanticText=s.SemanticText,DataType=s.DataType,
    Cardinality=s.Cardinality,UnitFamily=s.UnitFamily,ValidationRegex=s.Regex,MinNumericValue=s.MinValue,
    MaxNumericValue=s.MaxValue,MaxLength=s.MaxLength,IsGoogleMerchantAttribute=s.IsGoogle,
    IsVariantAxis=s.IsVariant,IsSearchable=s.IsSearchable,IsFilterable=s.IsFilterable,
    IsRequiredByDefault=s.IsRequired,DisplayOrder=s.DisplayOrder,UpdatedAt=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(AttributeDefinitionId,AttributeGroupId,Code,GoogleAttributeName,Name,Description,SemanticText,DataType,Cardinality,UnitFamily,ValidationRegex,MinNumericValue,MaxNumericValue,MaxLength,IsGoogleMerchantAttribute,IsVariantAxis,IsSearchable,IsFilterable,IsRequiredByDefault,DisplayOrder)
VALUES(s.Id,s.GroupId,s.Code,s.GoogleName,s.Name,s.Description,s.SemanticText,s.DataType,s.Cardinality,s.UnitFamily,s.Regex,s.MinValue,s.MaxValue,s.MaxLength,s.IsGoogle,s.IsVariant,s.IsSearchable,s.IsFilterable,s.IsRequired,s.DisplayOrder);

/* P2 = 3.5mm TRS (stereo audio), P3 = 3.5mm TRRS (headset audio+mic),
   P10 = 6.35mm (instrument/professional audio). These are distinct connector
   sizes/pinouts and must not be conflated. */
MERGE Catalog.AttributeOptions AS t USING (VALUES
 (1901,69,'USB','USB',N'USB',N'usb conexão cabo padrão universal',10),
 (1902,69,'USB_C','USB-C',N'USB-C',N'usb-c usb tipo c conexão reversível',20),
 (1903,69,'BLUETOOTH','Bluetooth',N'Bluetooth',N'bluetooth sem fio wireless conexão',30),
 (1904,69,'WIFI','Wi-Fi',N'Wi-Fi',N'wifi wi-fi rede sem fio conexão wireless',40),
 (1905,69,'P2','P2',N'P2',N'p2 conector de áudio 3,5 mm 3.5 mm trs estéreo fone',50),
 (1906,69,'P3','P3',N'P3',N'p3 conector de áudio 3,5 mm 3.5 mm trrs headset fone com microfone',60),
 (1907,69,'P10','P10',N'P10',N'p10 conector de áudio 6,35 mm 6.35 mm jack grande instrumento áudio profissional',70),
 (1908,69,'XLR','XLR',N'XLR',N'xlr conector áudio profissional microfone',80),
 (1909,69,'HDMI','HDMI',N'HDMI',N'hdmi conexão vídeo áudio digital alta definição',90),
 (1910,69,'ETHERNET','Ethernet',N'Ethernet',N'ethernet rede cabo rj45 conexão com fio',100)
) s(Id,DefinitionId,Code,GoogleValue,Name,SemanticText,DisplayOrder)
ON t.AttributeOptionId = s.Id OR (t.AttributeDefinitionId = s.DefinitionId AND t.Code = s.Code)
WHEN MATCHED THEN UPDATE SET
    AttributeOptionId=s.Id,AttributeDefinitionId=s.DefinitionId,Code=s.Code,GoogleValue=s.GoogleValue,
    Name=s.Name,SemanticText=s.SemanticText,DisplayOrder=s.DisplayOrder
WHEN NOT MATCHED THEN INSERT(AttributeOptionId,AttributeDefinitionId,Code,GoogleValue,Name,SemanticText,DisplayOrder)
VALUES(s.Id,s.DefinitionId,s.Code,s.GoogleValue,s.Name,s.SemanticText,s.DisplayOrder);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
