/*
 Yunu.Commerce - SKU attribute catalog: connection_type attribute (SQL Server)
 Adds a semantically dedicated Enum attribute definition for connection/
 interface type (e.g. USB, USB-C, Bluetooth, P2, P3, XLR, HDMI, Ethernet),
 replacing the previous gap where "conexão = USB" had no adequate attribute
 definition and risked being mapped into the generic Json product_detail
 attribute. Idempotent migration: safe to execute more than once.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* AttributeGroupId 3 = VARIANT (see 002_create_sku_attribute_catalog.sql);
   connection type can differentiate SKUs (e.g. USB vs Bluetooth versions of
   the same product), so it belongs alongside color/size/material. */
MERGE Catalog.AttributeDefinitions AS t USING (VALUES
 (69,3,'connection_type','connectivity',N'Tipo de conexão',N'Interface ou tipo de conexão física ou sem fio do item.',N'conexão interface conector usb usb-c bluetooth p2 p3 xlr hdmi ethernet tipo de conexão','Enum','Multiple',NULL,NULL,NULL,NULL,NULL,1,1,1,1,0,140)
) s(Id,GroupId,Code,GoogleName,Name,Description,SemanticText,DataType,Cardinality,UnitFamily,Regex,MinValue,MaxValue,MaxLength,IsGoogle,IsVariant,IsSearchable,IsFilterable,IsRequired,DisplayOrder)
ON t.AttributeDefinitionId=s.Id
WHEN MATCHED THEN UPDATE SET AttributeGroupId=s.GroupId,Code=s.Code,GoogleAttributeName=s.GoogleName,Name=s.Name,Description=s.Description,SemanticText=s.SemanticText,DataType=s.DataType,Cardinality=s.Cardinality,UnitFamily=s.UnitFamily,ValidationRegex=s.Regex,MinNumericValue=s.MinValue,MaxNumericValue=s.MaxValue,MaxLength=s.MaxLength,IsGoogleMerchantAttribute=s.IsGoogle,IsVariantAxis=s.IsVariant,IsSearchable=s.IsSearchable,IsFilterable=s.IsFilterable,IsRequiredByDefault=s.IsRequired,DisplayOrder=s.DisplayOrder,UpdatedAt=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(AttributeDefinitionId,AttributeGroupId,Code,GoogleAttributeName,Name,Description,SemanticText,DataType,Cardinality,UnitFamily,ValidationRegex,MinNumericValue,MaxNumericValue,MaxLength,IsGoogleMerchantAttribute,IsVariantAxis,IsSearchable,IsFilterable,IsRequiredByDefault,DisplayOrder)
VALUES(s.Id,s.GroupId,s.Code,s.GoogleName,s.Name,s.Description,s.SemanticText,s.DataType,s.Cardinality,s.UnitFamily,s.Regex,s.MinValue,s.MaxValue,s.MaxLength,s.IsGoogle,s.IsVariant,s.IsSearchable,s.IsFilterable,s.IsRequired,s.DisplayOrder);

MERGE Catalog.AttributeOptions AS t USING (VALUES
 (1901,69,'USB','USB',N'USB',N'usb conexão cabo padrão universal',10),
 (1902,69,'USB_C','USB-C',N'USB-C',N'usb-c usb tipo c conexão reversível',20),
 (1903,69,'BLUETOOTH','Bluetooth',N'Bluetooth',N'bluetooth sem fio wireless conexão',30),
 (1904,69,'P2','P2',N'P2',N'p2 conector áudio 3.5mm mini jack',40),
 (1905,69,'P3','P3',N'P3',N'p3 conector áudio 6.35mm jack grande',50),
 (1906,69,'XLR','XLR',N'XLR',N'xlr conector áudio profissional microfone',60),
 (1907,69,'HDMI','HDMI',N'HDMI',N'hdmi conexão vídeo áudio digital alta definição',70),
 (1908,69,'ETHERNET','Ethernet',N'Ethernet',N'ethernet rede cabo rj45 conexão com fio',80),
 (1909,69,'WIFI','Wi-Fi',N'Wi-Fi',N'wifi wi-fi rede sem fio conexão wireless',90)
) s(Id,DefinitionId,Code,GoogleValue,Name,SemanticText,DisplayOrder)
ON t.AttributeOptionId=s.Id WHEN MATCHED THEN UPDATE SET AttributeDefinitionId=s.DefinitionId,Code=s.Code,GoogleValue=s.GoogleValue,Name=s.Name,SemanticText=s.SemanticText,DisplayOrder=s.DisplayOrder
WHEN NOT MATCHED THEN INSERT(AttributeOptionId,AttributeDefinitionId,Code,GoogleValue,Name,SemanticText,DisplayOrder) VALUES(s.Id,s.DefinitionId,s.Code,s.GoogleValue,s.Name,s.SemanticText,s.DisplayOrder);

COMMIT TRANSACTION;
