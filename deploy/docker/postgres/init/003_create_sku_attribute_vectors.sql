/* Yunu.Commerce - yunu_vectors.public SKU attribute embeddings.
   Mirrors public.google_taxonomy_embeddings. SQL Server remains source of truth.
   vector(1536) must match the Azure OpenAI deployment. */
BEGIN;
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.sku_attribute_embeddings (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 entity_type varchar(30) NOT NULL DEFAULT 'AttributeDefinition'
   CHECK (entity_type IN ('AttributeDefinition','AttributeOption','SkuAttributeValue')),
 entity_id varchar(100) NOT NULL,
 attribute_code varchar(100) NOT NULL,
 option_code varchar(100),
 google_category_id bigint,
 sku_id uuid,
 locale varchar(10) NOT NULL DEFAULT 'pt-BR',
 name varchar(200) NOT NULL,
 semantic_text text NOT NULL,
 embedding vector(1536),
 embedding_model varchar(150),
 content_hash char(64) NOT NULL,
 embedded_content_hash char(64),
 metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
 source_updated_at timestamptz,
 embedded_at timestamptz,
 is_active boolean NOT NULL DEFAULT true,
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 CONSTRAINT uq_sku_attribute_embeddings_source UNIQUE(entity_type,entity_id,locale)
);

CREATE INDEX IF NOT EXISTS ix_sku_attribute_embeddings_attribute_code ON public.sku_attribute_embeddings(attribute_code);
CREATE INDEX IF NOT EXISTS ix_sku_attribute_embeddings_google_category_id ON public.sku_attribute_embeddings(google_category_id) WHERE google_category_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_sku_attribute_embeddings_sku_id ON public.sku_attribute_embeddings(sku_id) WHERE sku_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_sku_attribute_embeddings_metadata ON public.sku_attribute_embeddings USING gin(metadata);
CREATE INDEX IF NOT EXISTS ix_sku_attribute_embeddings_vector_cosine ON public.sku_attribute_embeddings
 USING hnsw (embedding vector_cosine_ops) WHERE embedding IS NOT NULL;

CREATE OR REPLACE VIEW public.pending_sku_attribute_embeddings AS
SELECT id,entity_type,entity_id,attribute_code,option_code,google_category_id,sku_id,locale,name,semantic_text,content_hash,metadata
FROM public.sku_attribute_embeddings
WHERE is_active AND (embedding IS NULL OR embedded_content_hash IS DISTINCT FROM content_hash);

CREATE OR REPLACE FUNCTION public.upsert_sku_attribute_embedding_source(
 p_entity_type varchar,p_entity_id varchar,p_attribute_code varchar,p_name varchar,p_semantic_text text,
 p_option_code varchar DEFAULT NULL,p_google_category_id bigint DEFAULT NULL,p_sku_id uuid DEFAULT NULL,
 p_locale varchar DEFAULT 'pt-BR',p_metadata jsonb DEFAULT '{}'::jsonb,p_source_updated_at timestamptz DEFAULT NULL)
RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_hash char(64); v_id uuid;
BEGIN
 v_hash:=encode(digest(convert_to(p_semantic_text,'UTF8'),'sha256'),'hex');
 INSERT INTO public.sku_attribute_embeddings(entity_type,entity_id,attribute_code,option_code,google_category_id,sku_id,locale,name,semantic_text,content_hash,metadata,source_updated_at)
 VALUES(p_entity_type,p_entity_id,p_attribute_code,p_option_code,p_google_category_id,p_sku_id,p_locale,p_name,p_semantic_text,v_hash,COALESCE(p_metadata,'{}'::jsonb),p_source_updated_at)
 ON CONFLICT(entity_type,entity_id,locale) DO UPDATE SET attribute_code=EXCLUDED.attribute_code,option_code=EXCLUDED.option_code,
  google_category_id=EXCLUDED.google_category_id,sku_id=EXCLUDED.sku_id,name=EXCLUDED.name,semantic_text=EXCLUDED.semantic_text,
  content_hash=EXCLUDED.content_hash,metadata=EXCLUDED.metadata,source_updated_at=EXCLUDED.source_updated_at,is_active=true,updated_at=now()
 RETURNING id INTO v_id;
 RETURN v_id;
END $$;

WITH seed(attribute_code,name,semantic_text) AS (VALUES
 ('gtin','GTIN','GTIN, EAN, UPC, ISBN ou código de barras: identificador global do SKU.'),
 ('mpn','MPN','MPN, referência ou número da peça definido pelo fabricante.'),
 ('brand','Marca','Marca, fabricante ou nome comercial do produto.'),
 ('identifier_exists','Possui identificador','Indica se existem GTIN, MPN e marca válidos.'),
 ('item_group_id','Grupo de variantes','Identificador compartilhado por variantes do mesmo produto.'),
 ('title','Título do SKU','Nome claro e específico usado em busca e anúncio.'),
 ('description','Descrição','Descrição factual com características, benefícios e finalidade.'),
 ('link','Link do produto','Endereço da página de compra do produto.'),
 ('image_link','Imagem principal','Foto ou imagem principal do produto.'),
 ('additional_image_link','Imagens adicionais','Fotos adicionais, detalhes e diferentes ângulos.'),
 ('video_link','Vídeos','Vídeos de apresentação, demonstração ou uso.'),
 ('product_highlight','Destaques','Principais benefícios e diferenciais do produto.'),
 ('product_detail','Detalhe técnico','Ficha técnica, especificação, seção, nome e valor.'),
 ('color','Cor','Cor ou tonalidade: preto, branco, azul, vermelho e combinações.'),
 ('size','Tamanho','Tamanho, numeração ou medida específica da variante.'),
 ('size_system','Sistema de tamanho','Sistema brasileiro, americano, europeu ou britânico.'),
 ('size_type','Tipo de corte','Modelagem regular, petite, plus size, tall, big ou gestante.'),
 ('material','Material','Material e composição: algodão, couro, poliéster, metal ou madeira.'),
 ('pattern','Estampa','Estampa ou padrão visual: liso, listrado, xadrez ou floral.'),
 ('scent','Fragrância','Aroma ou fragrância que diferencia a variante.'),
 ('flavor','Sabor','Sabor que diferencia alimento, bebida ou suplemento.'),
 ('capacity','Capacidade','Volume ou capacidade comercial em ml, l e outras unidades.'),
 ('model','Modelo','Modelo, versão, linha ou edição do item.'),
 ('voltage','Voltagem','Tensão elétrica 110 V, 127 V, 220 V ou bivolt.'),
 ('storage_capacity','Armazenamento','Capacidade digital em MB, GB ou TB.'),
 ('quantity_per_package','Quantidade por embalagem','Quantidade de unidades contidas na embalagem.'),
 ('condition','Condição','Produto novo, usado ou recondicionado.'),
 ('availability','Disponibilidade','Em estoque, indisponível, pré-venda ou sob encomenda.'),
 ('availability_date','Data de disponibilidade','Data de lançamento ou disponibilidade da pré-venda.'),
 ('price','Preço','Preço normal de venda e moeda do SKU.'),
 ('sale_price','Preço promocional','Preço com promoção, oferta ou desconto.'),
 ('sale_price_effective_date','Período promocional','Início e fim da vigência do preço promocional.'),
 ('cost_of_goods_sold','Custo do produto','Custo da mercadoria para cálculo de margem.'),
 ('multipack','Multipack','Pacote com múltiplas unidades idênticas.'),
 ('is_bundle','É kit','Kit ou combo de produtos diferentes vendidos juntos.'),
 ('adult','Conteúdo adulto','Produto destinado exclusivamente ao público adulto.'),
 ('product_weight','Peso do produto','Peso físico em gramas ou quilogramas.'),
 ('product_length','Comprimento','Comprimento ou profundidade física do item.'),
 ('product_width','Largura','Largura física do item.'),
 ('product_height','Altura','Altura física do item.'),
 ('shipping_weight','Peso para frete','Peso da embalagem usado no cálculo de entrega.'),
 ('shipping_length','Comprimento da embalagem','Comprimento do pacote para frete.'),
 ('shipping_width','Largura da embalagem','Largura do pacote para frete.'),
 ('shipping_height','Altura da embalagem','Altura do pacote para frete.'),
 ('min_handling_time','Prazo mínimo de manuseio','Mínimo de dias úteis para preparar e expedir.'),
 ('max_handling_time','Prazo máximo de manuseio','Máximo de dias úteis para preparar e expedir.'),
 ('shipping_label','Rótulo de frete','Grupo usado para aplicar regras de entrega.'),
 ('return_policy_label','Rótulo de devolução','Grupo usado para política de troca e devolução.'),
 ('google_product_category','Categoria Google','Categoria oficial da taxonomia Google Shopping.'),
 ('product_type','Tipo interno','Departamento, categoria, subcategoria e família do lojista.'),
 ('gender','Gênero','Público masculino, feminino ou unissex.'),
 ('age_group','Faixa etária','Recém-nascido, bebê, infantil ou adulto.'),
 ('unit_pricing_measure','Medida para preço unitário','Peso, volume, área, comprimento ou quantidade total.'),
 ('unit_pricing_base_measure','Medida base do preço unitário','Base para preço por kg, litro, metro ou unidade.'),
 ('compatibility','Compatibilidade','Modelos, aparelhos ou sistemas compatíveis.'),
 ('occasion','Ocasião','Uso casual, festa, esporte, trabalho ou presente.'),
 ('style','Estilo','Estilo moderno, clássico, esportivo, casual ou minimalista.'),
 ('feature','Característica','Recurso, propriedade ou diferencial pesquisável.'),
 ('energy_efficiency_class','Eficiência energética','Classe de eficiência e consumo energético.'),
 ('min_energy_efficiency_class','Classe energética mínima','Limite inferior da escala energética.'),
 ('max_energy_efficiency_class','Classe energética máxima','Limite superior da escala energética.'),
 ('certification','Certificação','Selo, norma ou registro como Inmetro e Anatel.'),
 ('question_and_answer','Perguntas e respostas','Dúvidas e respostas sobre uso, compatibilidade e manutenção.'),
 ('item_group_title','Título do grupo','Nome comum do produto pai que reúne variantes.'),
 ('document_link','Documentos','Manuais, guias, instruções e fichas em PDF.'),
 ('variant_option','Opções de variante','Combinações de cor, tamanho, material, modelo e outros eixos.'),
 ('related_product','Produtos relacionados','Acessórios, complementos, substitutos e itens compatíveis.'),
 ('popularity_rank','Popularidade','Ranking de procura, vendas ou destaque no catálogo.')
)
INSERT INTO public.sku_attribute_embeddings(entity_type,entity_id,attribute_code,name,semantic_text,content_hash,metadata)
SELECT 'AttributeDefinition',attribute_code,attribute_code,name,semantic_text,
 encode(digest(convert_to(semantic_text,'UTF8'),'sha256'),'hex'),
 jsonb_build_object('migration','00003','source','YunuCommerce.dbo.SkuAttributeDefinitions')
FROM seed
ON CONFLICT(entity_type,entity_id,locale) DO UPDATE SET name=EXCLUDED.name,semantic_text=EXCLUDED.semantic_text,
 content_hash=EXCLUDED.content_hash,metadata=EXCLUDED.metadata,updated_at=now();

COMMIT;

/* SELECT attribute_code,name,1-(embedding <=> :query_embedding::vector) AS similarity
   FROM public.sku_attribute_embeddings WHERE is_active AND embedding IS NOT NULL
   ORDER BY embedding <=> :query_embedding::vector LIMIT 10; */
