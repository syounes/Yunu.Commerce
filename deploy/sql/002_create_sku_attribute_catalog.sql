/*
 Yunu.Commerce - SKU attribute catalog (SQL Server)
 Structure identifiers are English; user-facing seed data is pt-BR.
 Source model: Google Merchant Center Product Data Specification / Merchant API v1.
 This database is the transactional source of truth. Embeddings belong in pgvector.
 Idempotent migration: safe to execute more than once.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'Catalog') IS NULL EXEC(N'CREATE SCHEMA Catalog');
IF SCHEMA_ID(N'Integration') IS NULL EXEC(N'CREATE SCHEMA Integration');

IF OBJECT_ID(N'Catalog.AttributeGroups', N'U') IS NULL
CREATE TABLE Catalog.AttributeGroups (
    AttributeGroupId smallint NOT NULL CONSTRAINT PK_AttributeGroups PRIMARY KEY,
    Code varchar(50) NOT NULL CONSTRAINT UQ_AttributeGroups_Code UNIQUE,
    Name nvarchar(100) NOT NULL,
    Description nvarchar(500) NULL,
    DisplayOrder smallint NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_AttributeGroups_IsActive DEFAULT (1)
);

IF OBJECT_ID(N'Catalog.AttributeDefinitions', N'U') IS NULL
CREATE TABLE Catalog.AttributeDefinitions (
    AttributeDefinitionId int NOT NULL CONSTRAINT PK_AttributeDefinitions PRIMARY KEY,
    AttributeGroupId smallint NOT NULL,
    Code varchar(100) NOT NULL CONSTRAINT UQ_AttributeDefinitions_Code UNIQUE,
    GoogleAttributeName varchar(100) NULL,
    Name nvarchar(150) NOT NULL,
    Description nvarchar(1000) NOT NULL,
    SemanticText nvarchar(2000) NOT NULL,
    DataType varchar(20) NOT NULL,
    Cardinality varchar(10) NOT NULL CONSTRAINT DF_AttributeDefinitions_Cardinality DEFAULT ('Single'),
    UnitFamily varchar(30) NULL,
    ValidationRegex nvarchar(500) NULL,
    MinNumericValue decimal(19,6) NULL,
    MaxNumericValue decimal(19,6) NULL,
    MaxLength int NULL,
    IsGoogleMerchantAttribute bit NOT NULL CONSTRAINT DF_AttributeDefinitions_IsGoogle DEFAULT (1),
    IsVariantAxis bit NOT NULL CONSTRAINT DF_AttributeDefinitions_IsVariant DEFAULT (0),
    IsSearchable bit NOT NULL CONSTRAINT DF_AttributeDefinitions_IsSearchable DEFAULT (1),
    IsFilterable bit NOT NULL CONSTRAINT DF_AttributeDefinitions_IsFilterable DEFAULT (0),
    IsRequiredByDefault bit NOT NULL CONSTRAINT DF_AttributeDefinitions_IsRequired DEFAULT (0),
    DisplayOrder smallint NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_AttributeDefinitions_IsActive DEFAULT (1),
    CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_AttributeDefinitions_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_AttributeDefinitions_UpdatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_AttributeDefinitions_Group FOREIGN KEY (AttributeGroupId) REFERENCES Catalog.AttributeGroups(AttributeGroupId),
    CONSTRAINT CK_AttributeDefinitions_DataType CHECK (DataType IN ('Text','Integer','Decimal','Boolean','DateTime','Money','Measurement','Url','Enum','Json')),
    CONSTRAINT CK_AttributeDefinitions_Cardinality CHECK (Cardinality IN ('Single','Multiple'))
);

IF OBJECT_ID(N'Catalog.AttributeOptions', N'U') IS NULL
CREATE TABLE Catalog.AttributeOptions (
    AttributeOptionId int NOT NULL CONSTRAINT PK_AttributeOptions PRIMARY KEY,
    AttributeDefinitionId int NOT NULL,
    Code varchar(100) NOT NULL,
    GoogleValue varchar(100) NULL,
    Name nvarchar(150) NOT NULL,
    SemanticText nvarchar(1000) NOT NULL,
    DisplayOrder smallint NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_AttributeOptions_IsActive DEFAULT (1),
    CONSTRAINT FK_AttributeOptions_Definition FOREIGN KEY (AttributeDefinitionId) REFERENCES Catalog.AttributeDefinitions(AttributeDefinitionId),
    CONSTRAINT UQ_AttributeOptions_Definition_Code UNIQUE (AttributeDefinitionId, Code)
);

IF OBJECT_ID(N'Catalog.GoogleCategoryAttributeRules', N'U') IS NULL
CREATE TABLE Catalog.GoogleCategoryAttributeRules (
    GoogleCategoryId bigint NOT NULL,
    AttributeDefinitionId int NOT NULL,
    RequirementLevel varchar(15) NOT NULL,
    IsVariantAxis bit NOT NULL CONSTRAINT DF_GoogleCategoryAttributeRules_IsVariant DEFAULT (0),
    CountryCode char(2) NOT NULL CONSTRAINT DF_GoogleCategoryAttributeRules_Country DEFAULT ('*'),
    Notes nvarchar(500) NULL,
    CONSTRAINT PK_GoogleCategoryAttributeRules PRIMARY KEY (GoogleCategoryId, AttributeDefinitionId, CountryCode),
    CONSTRAINT FK_GoogleCategoryAttributeRules_Definition FOREIGN KEY (AttributeDefinitionId) REFERENCES Catalog.AttributeDefinitions(AttributeDefinitionId),
    CONSTRAINT CK_GoogleCategoryAttributeRules_Level CHECK (RequirementLevel IN ('Required','Recommended','Optional'))
);

IF OBJECT_ID(N'Catalog.SkuAttributeValues', N'U') IS NULL
CREATE TABLE Catalog.SkuAttributeValues (
    SkuAttributeValueId uniqueidentifier NOT NULL CONSTRAINT PK_SkuAttributeValues PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    SkuId uniqueidentifier NOT NULL,
    AttributeDefinitionId int NOT NULL,
    Sequence smallint NOT NULL CONSTRAINT DF_SkuAttributeValues_Sequence DEFAULT (1),
    TextValue nvarchar(4000) NULL,
    IntegerValue bigint NULL,
    DecimalValue decimal(19,6) NULL,
    BooleanValue bit NULL,
    DateTimeValue datetime2(3) NULL,
    MoneyAmount decimal(19,4) NULL,
    CurrencyCode char(3) NULL,
    MeasurementValue decimal(19,6) NULL,
    UnitCode varchar(20) NULL,
    AttributeOptionId int NULL,
    JsonValue nvarchar(max) NULL,
    NormalizedText nvarchar(2000) NULL,
    Source varchar(30) NOT NULL CONSTRAINT DF_SkuAttributeValues_Source DEFAULT ('User'),
    Confidence decimal(5,4) NULL,
    CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_SkuAttributeValues_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_SkuAttributeValues_UpdatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_SkuAttributeValues_Definition FOREIGN KEY (AttributeDefinitionId) REFERENCES Catalog.AttributeDefinitions(AttributeDefinitionId),
    CONSTRAINT FK_SkuAttributeValues_Option FOREIGN KEY (AttributeOptionId) REFERENCES Catalog.AttributeOptions(AttributeOptionId),
    CONSTRAINT UQ_SkuAttributeValues UNIQUE (SkuId, AttributeDefinitionId, Sequence),
    CONSTRAINT CK_SkuAttributeValues_Source CHECK (Source IN ('User','Import','AI','Google','System')),
    CONSTRAINT CK_SkuAttributeValues_Confidence CHECK (Confidence IS NULL OR (Confidence >= 0 AND Confidence <= 1)),
    CONSTRAINT CK_SkuAttributeValues_Json CHECK (JsonValue IS NULL OR ISJSON(JsonValue) = 1),
    CONSTRAINT CK_SkuAttributeValues_HasValue CHECK (
      TextValue IS NOT NULL OR IntegerValue IS NOT NULL OR DecimalValue IS NOT NULL OR BooleanValue IS NOT NULL OR
      DateTimeValue IS NOT NULL OR MoneyAmount IS NOT NULL OR MeasurementValue IS NOT NULL OR AttributeOptionId IS NOT NULL OR JsonValue IS NOT NULL)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SkuAttributeValues_SkuId' AND object_id=OBJECT_ID(N'Catalog.SkuAttributeValues'))
 CREATE INDEX IX_SkuAttributeValues_SkuId ON Catalog.SkuAttributeValues(SkuId) INCLUDE(AttributeDefinitionId, AttributeOptionId, NormalizedText);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_SkuAttributeValues_Search' AND object_id=OBJECT_ID(N'Catalog.SkuAttributeValues'))
 CREATE INDEX IX_SkuAttributeValues_Search ON Catalog.SkuAttributeValues(AttributeDefinitionId, AttributeOptionId) INCLUDE(SkuId, TextValue, DecimalValue);

IF OBJECT_ID(N'Integration.AttributeEmbeddingOutbox', N'U') IS NULL
CREATE TABLE Integration.AttributeEmbeddingOutbox (
    EventId uniqueidentifier NOT NULL CONSTRAINT PK_AttributeEmbeddingOutbox PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    EntityType varchar(30) NOT NULL,
    EntityId varchar(100) NOT NULL,
    Operation varchar(20) NOT NULL,
    ContentHash char(64) NOT NULL,
    OccurredAt datetime2(3) NOT NULL CONSTRAINT DF_AttributeEmbeddingOutbox_OccurredAt DEFAULT SYSUTCDATETIME(),
    ProcessedAt datetime2(3) NULL,
    RetryCount smallint NOT NULL CONSTRAINT DF_AttributeEmbeddingOutbox_Retry DEFAULT (0),
    LastError nvarchar(2000) NULL,
    CONSTRAINT CK_AttributeEmbeddingOutbox_Entity CHECK (EntityType IN ('AttributeDefinition','AttributeOption','SkuAttributeValue')),
    CONSTRAINT CK_AttributeEmbeddingOutbox_Operation CHECK (Operation IN ('Upsert','Delete'))
);

MERGE Catalog.AttributeGroups AS t USING (VALUES
 (1,'IDENTIFICATION',N'Identificação',N'Identificadores e agrupamento de variantes.',10),
 (2,'CONTENT',N'Conteúdo',N'Textos, mídia e informações descritivas.',20),
 (3,'VARIANT',N'Variações',N'Eixos que diferenciam SKUs do mesmo produto.',30),
 (4,'COMMERCIAL',N'Comercial',N'Preço, condição, disponibilidade e campanhas.',40),
 (5,'LOGISTICS',N'Logística',N'Peso, dimensões, entrega e retirada.',50),
 (6,'CLASSIFICATION',N'Classificação',N'Categorias, público e características classificatórias.',60),
 (7,'SUSTAINABILITY',N'Sustentabilidade e conformidade',N'Energia, certificações e informações regulatórias.',70),
 (8,'CONVERSATIONAL',N'IA conversacional',N'Contexto adicional para descoberta por agentes e busca semântica.',80)
) s(Id,Code,Name,Description,DisplayOrder)
ON t.AttributeGroupId=s.Id WHEN MATCHED THEN UPDATE SET Code=s.Code,Name=s.Name,Description=s.Description,DisplayOrder=s.DisplayOrder
WHEN NOT MATCHED THEN INSERT(AttributeGroupId,Code,Name,Description,DisplayOrder) VALUES(s.Id,s.Code,s.Name,s.Description,s.DisplayOrder);

/* DataType: Text, Integer, Decimal, Boolean, DateTime, Money, Measurement, Url, Enum or Json. */
MERGE Catalog.AttributeDefinitions AS t USING (VALUES
 (1,1,'gtin','gtin',N'GTIN',N'Código global do item comercial. Aceita GTIN-8, GTIN-12, GTIN-13 ou GTIN-14.',N'gtin código de barras ean upc isbn identificador global comercial do sku','Text','Multiple',NULL,N'^[0-9]{8}$|^[0-9]{12,14}$',NULL,NULL,14,1,0,0,0,0,10),
 (2,1,'mpn','mpn',N'MPN',N'Número da peça definido pelo fabricante.',N'mpn código referência número da peça modelo fabricante do sku','Text','Single',NULL,NULL,NULL,NULL,70,1,0,1,1,0,20),
 (3,1,'brand','brand',N'Marca',N'Marca comercial do item.',N'marca fabricante brand nome comercial do produto e sku','Text','Single',NULL,NULL,NULL,NULL,70,1,0,1,1,0,30),
 (4,1,'identifier_exists','identifier_exists',N'Possui identificador',N'Indica se existem identificadores únicos adequados, como GTIN, MPN e marca.',N'produto possui gtin mpn marca identificador único verdadeiro falso','Boolean','Single',NULL,NULL,NULL,NULL,NULL,1,0,0,1,0,40),
 (5,1,'item_group_id','item_group_id',N'Grupo de variantes',N'Identificador compartilhado entre variantes do mesmo produto.',N'grupo família variações variantes skus do mesmo produto item group id','Text','Single',NULL,NULL,NULL,NULL,50,1,0,1,1,0,50),
 (6,2,'title','title',N'Título do SKU',N'Título claro e específico do item.',N'título nome descrição curta produto sku para pesquisa e anúncio','Text','Single',NULL,NULL,NULL,NULL,150,1,0,1,0,1,10),
 (7,2,'description','description',N'Descrição',N'Descrição completa e factual do item.',N'descrição detalhes benefícios características produto sku','Text','Single',NULL,NULL,NULL,NULL,5000,1,0,1,0,1,20),
 (8,2,'link','link',N'Link do produto',N'URL da página do item na loja.',N'url link página produto comprar sku loja virtual','Url','Single',NULL,NULL,NULL,NULL,2048,1,0,0,0,1,30),
 (9,2,'image_link','image_link',N'Imagem principal',N'URL da imagem principal do item.',N'imagem foto principal produto sku visual','Url','Single',NULL,NULL,NULL,NULL,2048,1,0,0,0,1,40),
 (10,2,'additional_image_link','additional_image_link',N'Imagens adicionais',N'URLs de imagens adicionais do item.',N'fotos imagens adicionais ângulos detalhes do produto','Url','Multiple',NULL,NULL,NULL,NULL,2048,1,0,0,0,0,50),
 (11,2,'video_link','video_link',N'Vídeos',N'URLs de vídeos do item.',N'vídeo demonstração review apresentação produto','Url','Multiple',NULL,NULL,NULL,NULL,2048,1,0,1,0,0,60),
 (12,2,'product_highlight','product_highlight',N'Destaques',N'Benefícios e características mais relevantes em tópicos.',N'destaques benefícios diferenciais características principais produto','Text','Multiple',NULL,NULL,NULL,NULL,150,1,0,1,0,0,70),
 (13,2,'product_detail','product_detail',N'Detalhe técnico',N'Especificação estruturada com seção, nome e valor.',N'ficha técnica especificação detalhe seção atributo valor produto','Json','Multiple',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,80),
 (14,3,'color','color',N'Cor',N'Cor principal ou combinação de cores do SKU.',N'cor tonalidade color preto branco azul vermelho variante sku','Text','Single',NULL,NULL,NULL,NULL,100,1,1,1,1,0,10),
 (15,3,'size','size',N'Tamanho',N'Tamanho específico do SKU.',N'tamanho numeração medida size roupa calçado variante sku','Text','Single',NULL,NULL,NULL,NULL,100,1,1,1,1,0,20),
 (16,3,'size_system','size_system',N'Sistema de tamanho',N'Sistema usado para interpretar o tamanho.',N'sistema padrão país tamanho brasil us uk eu métrica','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,30),
 (17,3,'size_type','size_type',N'Tipo de corte',N'Tipo de modelagem ou corte da peça.',N'tipo tamanho corte regular petite plus tall maternity roupa','Enum','Multiple',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,40),
 (18,3,'material','material',N'Material',N'Material ou composição predominante do item.',N'material composição tecido couro algodão poliéster metal madeira','Text','Single',NULL,NULL,NULL,NULL,200,1,1,1,1,0,50),
 (19,3,'pattern','pattern',N'Estampa',N'Padrão visual ou estampa do item.',N'estampa padrão desenho liso listrado xadrez floral variante','Text','Single',NULL,NULL,NULL,NULL,100,1,1,1,1,0,60),
 (20,3,'scent','scent',N'Fragrância',N'Aroma ou fragrância que diferencia o SKU.',N'aroma fragrância perfume cheiro floral cítrico amadeirado variante','Text','Single',NULL,NULL,NULL,NULL,100,0,1,1,1,0,70),
 (21,3,'flavor','flavor',N'Sabor',N'Sabor que diferencia alimentos, bebidas ou suplementos.',N'sabor gosto chocolate baunilha morango alimento bebida variante','Text','Single',NULL,NULL,NULL,NULL,100,0,1,1,1,0,80),
 (22,3,'capacity','capacity',N'Capacidade',N'Volume ou capacidade comercial do SKU.',N'capacidade volume litros mililitros armazenamento quantidade variante','Measurement','Single','Volume',NULL,0,NULL,NULL,0,1,1,1,0,90),
 (23,4,'condition','condition',N'Condição',N'Estado comercial do item.',N'condição estado novo usado recondicionado produto','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,1,10),
 (24,4,'availability','availability',N'Disponibilidade',N'Situação de estoque e venda do item.',N'disponibilidade estoque disponível indisponível pré-venda encomenda','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,1,20),
 (25,4,'availability_date','availability_date',N'Data de disponibilidade',N'Data em que um item em pré-venda ficará disponível.',N'data lançamento disponibilidade entrega pré-venda preorder','DateTime','Single',NULL,NULL,NULL,NULL,NULL,1,0,0,0,0,30),
 (26,4,'price','price',N'Preço',N'Preço normal do SKU e moeda ISO 4217.',N'preço valor venda moeda reais brl produto sku','Money','Single',NULL,NULL,0,NULL,NULL,1,0,0,1,1,40),
 (27,4,'sale_price','sale_price',N'Preço promocional',N'Preço promocional vigente do SKU.',N'preço promoção desconto oferta valor promocional','Money','Single',NULL,NULL,0,NULL,NULL,1,0,0,1,0,50),
 (28,4,'sale_price_effective_date','sale_price_effective_date',N'Período promocional',N'Intervalo ISO 8601 de vigência do preço promocional.',N'início fim período vigência promoção preço desconto','Json','Single',NULL,NULL,NULL,NULL,NULL,1,0,0,0,0,60),
 (29,4,'cost_of_goods_sold','cost_of_goods_sold',N'Custo do produto',N'Custo do item para cálculo de margem bruta.',N'custo mercadoria produto vendido cmv margem lucro','Money','Single',NULL,NULL,0,NULL,NULL,1,0,0,0,0,70),
 (30,4,'multipack','multipack',N'Multipack',N'Número de produtos idênticos vendidos no pacote.',N'kit pacote multipack quantidade unidades idênticas','Integer','Single',NULL,NULL,2,NULL,NULL,1,0,1,1,0,80),
 (31,4,'is_bundle','is_bundle',N'É kit',N'Indica agrupamento de produtos diferentes vendido por um único preço.',N'kit combo bundle conjunto produtos diferentes','Boolean','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,90),
 (32,4,'adult','adult',N'Conteúdo adulto',N'Indica produto destinado a adultos.',N'adulto restrição idade conteúdo sensível produto','Boolean','Single',NULL,NULL,NULL,NULL,NULL,1,0,0,1,0,100),
 (33,5,'product_weight','product_weight',N'Peso do produto',N'Peso físico do item.',N'peso produto grama quilograma kg g','Measurement','Single','Weight',NULL,0,2000,NULL,1,0,1,1,0,10),
 (34,5,'product_length','product_length',N'Comprimento do produto',N'Comprimento físico do item.',N'comprimento profundidade dimensão produto centímetro','Measurement','Single','Length',NULL,0,3000,NULL,1,0,1,1,0,20),
 (35,5,'product_width','product_width',N'Largura do produto',N'Largura física do item.',N'largura dimensão produto centímetro','Measurement','Single','Length',NULL,0,3000,NULL,1,0,1,1,0,30),
 (36,5,'product_height','product_height',N'Altura do produto',N'Altura física do item.',N'altura dimensão produto centímetro','Measurement','Single','Length',NULL,0,3000,NULL,1,0,1,1,0,40),
 (37,5,'shipping_weight','shipping_weight',N'Peso para frete',N'Peso usado no cálculo de entrega.',N'peso embalagem frete transporte cálculo entrega','Measurement','Single','Weight',NULL,0,NULL,NULL,1,0,0,0,0,50),
 (38,5,'shipping_length','shipping_length',N'Comprimento da embalagem',N'Comprimento usado no cálculo de frete.',N'comprimento pacote embalagem frete transporte','Measurement','Single','Length',NULL,0,NULL,NULL,1,0,0,0,0,60),
 (39,5,'shipping_width','shipping_width',N'Largura da embalagem',N'Largura usada no cálculo de frete.',N'largura pacote embalagem frete transporte','Measurement','Single','Length',NULL,0,NULL,NULL,1,0,0,0,0,70),
 (40,5,'shipping_height','shipping_height',N'Altura da embalagem',N'Altura usada no cálculo de frete.',N'altura pacote embalagem frete transporte','Measurement','Single','Length',NULL,0,NULL,NULL,1,0,0,0,0,80),
 (41,5,'min_handling_time','min_handling_time',N'Prazo mínimo de manuseio',N'Dias úteis mínimos para preparar o pedido.',N'prazo mínimo preparação manuseio expedição dias úteis','Integer','Single',NULL,NULL,0,NULL,NULL,1,0,0,0,0,90),
 (42,5,'max_handling_time','max_handling_time',N'Prazo máximo de manuseio',N'Dias úteis máximos para preparar o pedido.',N'prazo máximo preparação manuseio expedição dias úteis','Integer','Single',NULL,NULL,0,NULL,NULL,1,0,0,0,0,100),
 (43,5,'shipping_label','shipping_label',N'Rótulo de frete',N'Rótulo para agrupar itens em regras de entrega.',N'etiqueta grupo regra frete entrega logística','Text','Single',NULL,NULL,NULL,NULL,100,1,0,0,1,0,110),
 (44,5,'return_policy_label','return_policy_label',N'Rótulo de devolução',N'Rótulo para vincular política de devolução.',N'etiqueta política devolução troca retorno','Text','Single',NULL,NULL,NULL,NULL,100,1,0,0,1,0,120),
 (45,6,'google_product_category','google_product_category',N'Categoria Google',N'Categoria oficial da taxonomia Google já sincronizada pelo Yunu.Commerce.',N'categoria taxonomia google shopping merchant classificação produto','Text','Single',NULL,NULL,NULL,NULL,750,1,0,1,1,0,10),
 (46,6,'product_type','product_type',N'Tipo interno do produto',N'Caminho hierárquico próprio do lojista.',N'departamento categoria subcategoria família tipo produto breadcrumb','Text','Multiple',NULL,NULL,NULL,NULL,750,1,0,1,1,0,20),
 (47,6,'gender','gender',N'Gênero',N'Público de gênero ao qual o item se destina.',N'gênero público masculino feminino unissex','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,30),
 (48,6,'age_group','age_group',N'Faixa etária',N'Faixa etária de destino do item.',N'idade faixa etária recém-nascido bebê infantil criança adulto','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,40),
 (49,6,'unit_pricing_measure','unit_pricing_measure',N'Medida para preço unitário',N'Medida e dimensão total do item para cálculo de preço unitário.',N'preço por unidade medida peso volume área comprimento','Measurement','Single','Generic',NULL,0,NULL,NULL,1,0,0,1,0,50),
 (50,6,'unit_pricing_base_measure','unit_pricing_base_measure',N'Medida base do preço unitário',N'Denominador usado para exibir o preço unitário.',N'base denominador preço por kg litro metro unidade','Measurement','Single','Generic',NULL,0,NULL,NULL,1,0,0,1,0,60),
 (51,7,'energy_efficiency_class','energy_efficiency_class',N'Classe de eficiência energética',N'Classe de eficiência energética do produto.',N'energia eficiência consumo classe a b c d e f g eletrodoméstico','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,10),
 (52,7,'min_energy_efficiency_class','min_energy_efficiency_class',N'Classe energética mínima',N'Limite inferior da escala de eficiência energética aplicável.',N'classe mínima escala eficiência energética','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,0,0,0,20),
 (53,7,'max_energy_efficiency_class','max_energy_efficiency_class',N'Classe energética máxima',N'Limite superior da escala de eficiência energética aplicável.',N'classe máxima escala eficiência energética','Enum','Single',NULL,NULL,NULL,NULL,NULL,1,0,0,0,0,30),
 (54,7,'certification','certification',N'Certificação',N'Certificação oficial ou regulatória do item.',N'certificação selo autoridade norma registro inmetro anatel eprel','Json','Multiple',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,40),
 (55,8,'question_and_answer','question_and_answer',N'Perguntas e respostas',N'Perguntas frequentes e respostas factuais sobre o produto.',N'pergunta resposta dúvida faq produto compatibilidade uso manutenção','Json','Multiple',NULL,NULL,NULL,NULL,NULL,1,0,1,0,0,10),
 (56,8,'item_group_title','item_group_title',N'Título do grupo',N'Título comum que descreve o conjunto de variantes.',N'título nome grupo variantes produto pai família skus','Text','Single',NULL,NULL,NULL,NULL,150,1,0,1,0,0,20),
 (57,8,'document_link','document_link',N'Documentos',N'URLs de manuais, guias, instruções ou documentos PDF.',N'manual guia instrução montagem ficha pdf documento produto','Url','Multiple',NULL,NULL,NULL,NULL,2048,1,0,1,0,0,30),
 (58,8,'variant_option','variant_option',N'Opções de variante',N'Lista estruturada das opções que identificam as variantes.',N'opções variação sku cor tamanho material modelo combinação','Json','Multiple',NULL,NULL,NULL,NULL,NULL,1,0,1,1,0,40),
 (59,8,'related_product','related_product',N'Produtos relacionados',N'Relações entre este SKU e outros produtos.',N'produto relacionado acessório substituto complemento compatível','Json','Multiple',NULL,NULL,NULL,NULL,NULL,1,0,1,0,0,50),
 (60,8,'popularity_rank','popularity_rank',N'Popularidade',N'Popularidade relativa no catálogo, de zero a cem.',N'ranking popularidade procura vendas destaque produto','Decimal','Single',NULL,NULL,0,100,NULL,1,0,1,1,0,60),
 (61,3,'model','model',N'Modelo',N'Nome ou código comercial do modelo.',N'modelo versão linha edição produto sku','Text','Single',NULL,NULL,NULL,NULL,150,0,1,1,1,0,100),
 (62,3,'voltage','voltage',N'Voltagem',N'Tensão elétrica nominal do SKU.',N'voltagem tensão elétrica volts 110 127 220 bivolt variante','Text','Single','Voltage',NULL,NULL,NULL,30,0,1,1,1,0,110),
 (63,3,'storage_capacity','storage_capacity',N'Armazenamento',N'Capacidade de armazenamento digital.',N'armazenamento memória capacidade gigabyte terabyte gb tb variante','Measurement','Single','DigitalStorage',NULL,0,NULL,NULL,0,1,1,1,0,120),
 (64,3,'quantity_per_package','quantity_per_package',N'Quantidade por embalagem',N'Quantidade comercial contida na embalagem.',N'quantidade unidades pacote caixa embalagem conteúdo','Decimal','Single','Count',NULL,0,NULL,NULL,0,1,1,1,0,130),
 (65,6,'compatibility','compatibility',N'Compatibilidade',N'Produtos, modelos ou sistemas com os quais o SKU é compatível.',N'compatível compatibilidade funciona com modelo aparelho sistema acessório','Text','Multiple',NULL,NULL,NULL,NULL,500,0,0,1,1,0,70),
 (66,6,'occasion','occasion',N'Ocasião',N'Contexto ou ocasião recomendada de uso.',N'ocasião uso festa casual esporte trabalho presente','Text','Multiple',NULL,NULL,NULL,NULL,100,0,0,1,1,0,80),
 (67,6,'style','style',N'Estilo',N'Estilo visual ou funcional do item.',N'estilo design moderno clássico esportivo casual minimalista','Text','Multiple',NULL,NULL,NULL,NULL,100,0,0,1,1,0,90),
 (68,6,'feature','feature',N'Característica',N'Característica adicional pesquisável não representada por atributo padrão.',N'característica recurso diferencial especificação propriedade produto','Text','Multiple',NULL,NULL,NULL,NULL,250,0,0,1,1,0,100)
) s(Id,GroupId,Code,GoogleName,Name,Description,SemanticText,DataType,Cardinality,UnitFamily,Regex,MinValue,MaxValue,MaxLength,IsGoogle,IsVariant,IsSearchable,IsFilterable,IsRequired,DisplayOrder)
ON t.AttributeDefinitionId=s.Id
WHEN MATCHED THEN UPDATE SET AttributeGroupId=s.GroupId,Code=s.Code,GoogleAttributeName=s.GoogleName,Name=s.Name,Description=s.Description,SemanticText=s.SemanticText,DataType=s.DataType,Cardinality=s.Cardinality,UnitFamily=s.UnitFamily,ValidationRegex=s.Regex,MinNumericValue=s.MinValue,MaxNumericValue=s.MaxValue,MaxLength=s.MaxLength,IsGoogleMerchantAttribute=s.IsGoogle,IsVariantAxis=s.IsVariant,IsSearchable=s.IsSearchable,IsFilterable=s.IsFilterable,IsRequiredByDefault=s.IsRequired,DisplayOrder=s.DisplayOrder,UpdatedAt=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(AttributeDefinitionId,AttributeGroupId,Code,GoogleAttributeName,Name,Description,SemanticText,DataType,Cardinality,UnitFamily,ValidationRegex,MinNumericValue,MaxNumericValue,MaxLength,IsGoogleMerchantAttribute,IsVariantAxis,IsSearchable,IsFilterable,IsRequiredByDefault,DisplayOrder)
VALUES(s.Id,s.GroupId,s.Code,s.GoogleName,s.Name,s.Description,s.SemanticText,s.DataType,s.Cardinality,s.UnitFamily,s.Regex,s.MinValue,s.MaxValue,s.MaxLength,s.IsGoogle,s.IsVariant,s.IsSearchable,s.IsFilterable,s.IsRequired,s.DisplayOrder);

MERGE Catalog.AttributeOptions AS t USING (VALUES
 (1001,16,'BR','BR',N'Brasil',N'sistema brasileiro de tamanhos numeração BR',10),(1002,16,'US','US',N'Estados Unidos',N'sistema americano de tamanhos US',20),(1003,16,'EU','EU',N'Europa',N'sistema europeu de tamanhos EU',30),(1004,16,'UK','UK',N'Reino Unido',N'sistema britânico de tamanhos UK',40),(1005,16,'MEX','MEX',N'México',N'sistema mexicano de tamanhos MEX',50),(1006,16,'AU','AU',N'Austrália',N'sistema australiano de tamanhos AU',60),(1007,16,'JP','JP',N'Japão',N'sistema japonês de tamanhos JP',70),(1008,16,'CN','CN',N'China',N'sistema chinês de tamanhos CN',80),
 (1101,17,'REGULAR','regular',N'Regular',N'corte tamanho regular padrão',10),(1102,17,'PETITE','petite',N'Petite',N'corte pequeno petite',20),(1103,17,'PLUS','plus',N'Plus size',N'tamanho grande plus size',30),(1104,17,'TALL','tall',N'Alto',N'corte para pessoas altas tall',40),(1105,17,'BIG','big',N'Grande',N'corte grande big',50),(1106,17,'MATERNITY','maternity',N'Gestante',N'modelagem gestante maternity',60),
 (1201,23,'NEW','new',N'Novo',N'produto novo lacrado sem uso',10),(1202,23,'USED','used',N'Usado',N'produto usado previamente',20),(1203,23,'REFURBISHED','refurbished',N'Recondicionado',N'produto recondicionado restaurado com garantia',30),
 (1301,24,'IN_STOCK','in_stock',N'Em estoque',N'disponível pronta entrega em estoque',10),(1302,24,'OUT_OF_STOCK','out_of_stock',N'Fora de estoque',N'indisponível sem estoque',20),(1303,24,'PREORDER','preorder',N'Pré-venda',N'pré-venda lançamento futuro',30),(1304,24,'BACKORDER','backorder',N'Sob encomenda',N'encomenda entrega futura backorder',40),(1305,24,'LIMITED_AVAILABILITY','limited_availability',N'Disponibilidade limitada',N'poucas unidades estoque limitado',50),
 (1401,47,'MALE','male',N'Masculino',N'produto para homem masculino',10),(1402,47,'FEMALE','female',N'Feminino',N'produto para mulher feminino',20),(1403,47,'UNISEX','unisex',N'Unissex',N'produto sem distinção de gênero unissex',30),
 (1501,48,'NEWBORN','newborn',N'Recém-nascido',N'recém-nascido zero a três meses',10),(1502,48,'INFANT','infant',N'Bebê',N'bebê três a doze meses',20),(1503,48,'TODDLER','toddler',N'Primeira infância',N'criança um a cinco anos',30),(1504,48,'KIDS','kids',N'Infantil',N'criança cinco a treze anos',40),(1505,48,'ADULT','adult',N'Adulto',N'adolescente e adulto',50),
 (1601,51,'A_PLUS_PLUS_PLUS','A+++',N'A+++',N'eficiência energética classe a mais mais mais',10),(1602,51,'A_PLUS_PLUS','A++',N'A++',N'eficiência energética classe a mais mais',20),(1603,51,'A_PLUS','A+',N'A+',N'eficiência energética classe a mais',30),(1604,51,'A','A',N'A',N'eficiência energética classe a',40),(1605,51,'B','B',N'B',N'eficiência energética classe b',50),(1606,51,'C','C',N'C',N'eficiência energética classe c',60),(1607,51,'D','D',N'D',N'eficiência energética classe d',70),(1608,51,'E','E',N'E',N'eficiência energética classe e',80),(1609,51,'F','F',N'F',N'eficiência energética classe f',90),(1610,51,'G','G',N'G',N'eficiência energética classe g',100),
 (1701,52,'A_PLUS_PLUS_PLUS','A+++',N'A+++',N'limite energético a mais mais mais',10),(1702,52,'A_PLUS_PLUS','A++',N'A++',N'limite energético a mais mais',20),(1703,52,'A_PLUS','A+',N'A+',N'limite energético a mais',30),(1704,52,'A','A',N'A',N'limite energético a',40),(1705,52,'B','B',N'B',N'limite energético b',50),(1706,52,'C','C',N'C',N'limite energético c',60),(1707,52,'D','D',N'D',N'limite energético d',70),(1708,52,'E','E',N'E',N'limite energético e',80),(1709,52,'F','F',N'F',N'limite energético f',90),(1710,52,'G','G',N'G',N'limite energético g',100),
 (1801,53,'A_PLUS_PLUS_PLUS','A+++',N'A+++',N'limite energético a mais mais mais',10),(1802,53,'A_PLUS_PLUS','A++',N'A++',N'limite energético a mais mais',20),(1803,53,'A_PLUS','A+',N'A+',N'limite energético a mais',30),(1804,53,'A','A',N'A',N'limite energético a',40),(1805,53,'B','B',N'B',N'limite energético b',50),(1806,53,'C','C',N'C',N'limite energético c',60),(1807,53,'D','D',N'D',N'limite energético d',70),(1808,53,'E','E',N'E',N'limite energético e',80),(1809,53,'F','F',N'F',N'limite energético f',90),(1810,53,'G','G',N'G',N'limite energético g',100)
) s(Id,DefinitionId,Code,GoogleValue,Name,SemanticText,DisplayOrder)
ON t.AttributeOptionId=s.Id WHEN MATCHED THEN UPDATE SET AttributeDefinitionId=s.DefinitionId,Code=s.Code,GoogleValue=s.GoogleValue,Name=s.Name,SemanticText=s.SemanticText,DisplayOrder=s.DisplayOrder
WHEN NOT MATCHED THEN INSERT(AttributeOptionId,AttributeDefinitionId,Code,GoogleValue,Name,SemanticText,DisplayOrder) VALUES(s.Id,s.DefinitionId,s.Code,s.GoogleValue,s.Name,s.SemanticText,s.DisplayOrder);

COMMIT TRANSACTION;

/* Optional FK after adapting the actual SKU table/schema:
ALTER TABLE Catalog.SkuAttributeValues ADD CONSTRAINT FK_SkuAttributeValues_Skus
FOREIGN KEY (SkuId) REFERENCES Catalog.Skus(SkuId);
*/
