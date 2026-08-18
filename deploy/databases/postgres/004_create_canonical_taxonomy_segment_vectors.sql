/*
    Yunu.Commerce - Canonical taxonomy and segment embeddings
    Target: PostgreSQL 16+ with pgvector
    Suggested path: deploy/init/postgres/00005_create_canonical_taxonomy_segment_vectors.sql

    Architecture:
      - SQL Server is the source of truth.
      - PostgreSQL + pgvector is a rebuildable semantic projection.
      - vector(1536) matches yunu-embedding-category-v1.
      - Source rows are upserted before embeddings are generated.
      - content_hash and embedded_content_hash prevent stale vectors from being used.
      - No business seed is inserted here. Synchronization reads SQL Server and
        populates this projection.

    Tables:
      1. public.canonical_taxonomy_embeddings
      2. public.segment_embeddings
*/

BEGIN;

CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

/* ============================================================================
   1. CANONICAL TAXONOMY NODE EMBEDDINGS
   ============================================================================ */

CREATE TABLE IF NOT EXISTS public.canonical_taxonomy_embeddings
(
    id                         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    canonical_taxonomy_node_id BIGINT NOT NULL,
    parent_node_id             BIGINT,
    node_code                  VARCHAR(120) NOT NULL,
    google_category_id         BIGINT,
    depth                      SMALLINT NOT NULL,
    path                       TEXT NOT NULL,
    locale                     VARCHAR(10) NOT NULL DEFAULT 'pt-BR',
    name                       VARCHAR(250) NOT NULL,
    semantic_text              TEXT NOT NULL,

    embedding                  vector(1536),
    embedding_provider         VARCHAR(50),
    embedding_model            VARCHAR(150),
    embedding_dimensions       SMALLINT NOT NULL DEFAULT 1536,

    content_hash               CHAR(64) NOT NULL,
    embedded_content_hash      CHAR(64),
    metadata                   JSONB NOT NULL DEFAULT '{}'::jsonb,
    source_updated_at          TIMESTAMPTZ,
    embedded_at                TIMESTAMPTZ,
    is_active                  BOOLEAN NOT NULL DEFAULT TRUE,
    created_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_canonical_taxonomy_embeddings_source
        UNIQUE (canonical_taxonomy_node_id, locale),

    CONSTRAINT ck_canonical_taxonomy_embeddings_node_id
        CHECK (canonical_taxonomy_node_id > 0),

    CONSTRAINT ck_canonical_taxonomy_embeddings_parent_id
        CHECK (parent_node_id IS NULL OR parent_node_id > 0),

    CONSTRAINT ck_canonical_taxonomy_embeddings_google_category_id
        CHECK (google_category_id IS NULL OR google_category_id > 0),

    CONSTRAINT ck_canonical_taxonomy_embeddings_depth
        CHECK (depth >= 0),

    CONSTRAINT ck_canonical_taxonomy_embeddings_node_code
        CHECK (BTRIM(node_code) <> ''),

    CONSTRAINT ck_canonical_taxonomy_embeddings_path
        CHECK (BTRIM(path) <> ''),

    CONSTRAINT ck_canonical_taxonomy_embeddings_locale
        CHECK (BTRIM(locale) <> ''),

    CONSTRAINT ck_canonical_taxonomy_embeddings_content_hash
        CHECK (content_hash ~ '^[0-9a-f]{64}$'),

    CONSTRAINT ck_canonical_taxonomy_embeddings_embedded_hash
        CHECK
        (
            embedded_content_hash IS NULL
            OR embedded_content_hash ~ '^[0-9a-f]{64}$'
        ),

    CONSTRAINT ck_canonical_taxonomy_embeddings_dimensions
        CHECK (embedding_dimensions = 1536),

    CONSTRAINT ck_canonical_taxonomy_embeddings_vector_state
        CHECK
        (
            (
                embedding IS NULL
                AND embedded_content_hash IS NULL
                AND embedded_at IS NULL
            )
            OR
            (
                embedding IS NOT NULL
                AND embedding_provider IS NOT NULL
                AND embedding_model IS NOT NULL
                AND embedded_content_hash IS NOT NULL
                AND embedded_at IS NOT NULL
            )
        )
);

CREATE INDEX IF NOT EXISTS ix_canonical_taxonomy_embeddings_node_code
    ON public.canonical_taxonomy_embeddings (node_code);

CREATE INDEX IF NOT EXISTS ix_canonical_taxonomy_embeddings_parent_node_id
    ON public.canonical_taxonomy_embeddings (parent_node_id)
    WHERE parent_node_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_canonical_taxonomy_embeddings_google_category_id
    ON public.canonical_taxonomy_embeddings (google_category_id)
    WHERE google_category_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_canonical_taxonomy_embeddings_metadata
    ON public.canonical_taxonomy_embeddings
    USING gin (metadata);

CREATE INDEX IF NOT EXISTS ix_canonical_taxonomy_embeddings_vector_cosine
    ON public.canonical_taxonomy_embeddings
    USING hnsw (embedding vector_cosine_ops)
    WHERE is_active
      AND embedding IS NOT NULL
      AND embedded_content_hash = content_hash;

/* ============================================================================
   2. SEGMENT DEFINITION AND OPTION EMBEDDINGS

   One table is intentional. It follows the existing SKU attribute projection
   and permits independent vector searches for SegmentDefinition and
   SegmentOption while preserving the parent definition relationship.
   ============================================================================ */

CREATE TABLE IF NOT EXISTS public.segment_embeddings
(
    id                         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type                VARCHAR(30) NOT NULL,
    entity_id                  BIGINT NOT NULL,
    segment_definition_id      BIGINT NOT NULL,
    segment_option_id          BIGINT,
    segment_code               VARCHAR(100) NOT NULL,
    option_code                VARCHAR(100),
    locale                     VARCHAR(10) NOT NULL DEFAULT 'pt-BR',
    name                       VARCHAR(200) NOT NULL,
    semantic_text              TEXT NOT NULL,

    embedding                  vector(1536),
    embedding_provider         VARCHAR(50),
    embedding_model            VARCHAR(150),
    embedding_dimensions       SMALLINT NOT NULL DEFAULT 1536,

    content_hash               CHAR(64) NOT NULL,
    embedded_content_hash      CHAR(64),
    metadata                   JSONB NOT NULL DEFAULT '{}'::jsonb,
    source_updated_at          TIMESTAMPTZ,
    embedded_at                TIMESTAMPTZ,
    is_active                  BOOLEAN NOT NULL DEFAULT TRUE,
    created_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                 TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_segment_embeddings_source
        UNIQUE (entity_type, entity_id, locale),

    CONSTRAINT ck_segment_embeddings_entity_type
        CHECK (entity_type IN ('SegmentDefinition', 'SegmentOption')),

    CONSTRAINT ck_segment_embeddings_entity_id
        CHECK (entity_id > 0),

    CONSTRAINT ck_segment_embeddings_definition_id
        CHECK (segment_definition_id > 0),

    CONSTRAINT ck_segment_embeddings_entity_shape
        CHECK
        (
            (
                entity_type = 'SegmentDefinition'
                AND entity_id = segment_definition_id
                AND segment_option_id IS NULL
                AND option_code IS NULL
            )
            OR
            (
                entity_type = 'SegmentOption'
                AND segment_option_id IS NOT NULL
                AND segment_option_id > 0
                AND entity_id = segment_option_id
                AND option_code IS NOT NULL
                AND BTRIM(option_code) <> ''
            )
        ),

    CONSTRAINT ck_segment_embeddings_segment_code
        CHECK (BTRIM(segment_code) <> ''),

    CONSTRAINT ck_segment_embeddings_locale
        CHECK (BTRIM(locale) <> ''),

    CONSTRAINT ck_segment_embeddings_content_hash
        CHECK (content_hash ~ '^[0-9a-f]{64}$'),

    CONSTRAINT ck_segment_embeddings_embedded_hash
        CHECK
        (
            embedded_content_hash IS NULL
            OR embedded_content_hash ~ '^[0-9a-f]{64}$'
        ),

    CONSTRAINT ck_segment_embeddings_dimensions
        CHECK (embedding_dimensions = 1536),

    CONSTRAINT ck_segment_embeddings_vector_state
        CHECK
        (
            (
                embedding IS NULL
                AND embedded_content_hash IS NULL
                AND embedded_at IS NULL
            )
            OR
            (
                embedding IS NOT NULL
                AND embedding_provider IS NOT NULL
                AND embedding_model IS NOT NULL
                AND embedded_content_hash IS NOT NULL
                AND embedded_at IS NOT NULL
            )
        )
);

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_definition_id
    ON public.segment_embeddings (segment_definition_id, entity_type, is_active);

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_segment_code
    ON public.segment_embeddings (segment_code, locale);

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_option_code
    ON public.segment_embeddings (segment_definition_id, option_code, locale)
    WHERE entity_type = 'SegmentOption'
      AND option_code IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_metadata
    ON public.segment_embeddings
    USING gin (metadata);

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_definition_vector_cosine
    ON public.segment_embeddings
    USING hnsw (embedding vector_cosine_ops)
    WHERE entity_type = 'SegmentDefinition'
      AND is_active
      AND embedding IS NOT NULL
      AND embedded_content_hash = content_hash;

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_option_vector_cosine
    ON public.segment_embeddings
    USING hnsw (embedding vector_cosine_ops)
    WHERE entity_type = 'SegmentOption'
      AND is_active
      AND embedding IS NOT NULL
      AND embedded_content_hash = content_hash;

/* ============================================================================
   3. PENDING EMBEDDING VIEWS
   ============================================================================ */

CREATE OR REPLACE VIEW public.pending_canonical_taxonomy_embeddings AS
SELECT
    id,
    canonical_taxonomy_node_id,
    parent_node_id,
    node_code,
    google_category_id,
    depth,
    path,
    locale,
    name,
    semantic_text,
    content_hash,
    metadata,
    source_updated_at
FROM public.canonical_taxonomy_embeddings
WHERE is_active
  AND
  (
      embedding IS NULL
      OR embedded_content_hash IS DISTINCT FROM content_hash
  );

CREATE OR REPLACE VIEW public.pending_segment_embeddings AS
SELECT
    id,
    entity_type,
    entity_id,
    segment_definition_id,
    segment_option_id,
    segment_code,
    option_code,
    locale,
    name,
    semantic_text,
    content_hash,
    metadata,
    source_updated_at
FROM public.segment_embeddings
WHERE is_active
  AND
  (
      embedding IS NULL
      OR embedded_content_hash IS DISTINCT FROM content_hash
  );

/* ============================================================================
   4. SOURCE UPSERT FUNCTIONS

   The synchronization API calls these functions after reading SQL Server.
   They calculate SHA-256 consistently inside PostgreSQL. Existing embeddings
   remain stored after a content change but become ineligible for retrieval
   until regenerated because embedded_content_hash no longer matches.
   ============================================================================ */

CREATE OR REPLACE FUNCTION public.upsert_canonical_taxonomy_embedding_source
(
    p_canonical_taxonomy_node_id BIGINT,
    p_node_code VARCHAR,
    p_name VARCHAR,
    p_semantic_text TEXT,
    p_parent_node_id BIGINT DEFAULT NULL,
    p_google_category_id BIGINT DEFAULT NULL,
    p_depth SMALLINT DEFAULT 0,
    p_path TEXT DEFAULT '/',
    p_locale VARCHAR DEFAULT 'pt-BR',
    p_metadata JSONB DEFAULT '{}'::jsonb,
    p_source_updated_at TIMESTAMPTZ DEFAULT NULL
)
RETURNS UUID
LANGUAGE plpgsql
AS $$
DECLARE
    v_content_hash CHAR(64);
    v_id UUID;
BEGIN
    v_content_hash := encode
    (
        digest(convert_to(p_semantic_text, 'UTF8'), 'sha256'),
        'hex'
    );

    INSERT INTO public.canonical_taxonomy_embeddings
    (
        canonical_taxonomy_node_id,
        parent_node_id,
        node_code,
        google_category_id,
        depth,
        path,
        locale,
        name,
        semantic_text,
        content_hash,
        metadata,
        source_updated_at,
        is_active,
        updated_at
    )
    VALUES
    (
        p_canonical_taxonomy_node_id,
        p_parent_node_id,
        p_node_code,
        p_google_category_id,
        p_depth,
        p_path,
        p_locale,
        p_name,
        p_semantic_text,
        v_content_hash,
        COALESCE(p_metadata, '{}'::jsonb),
        p_source_updated_at,
        TRUE,
        NOW()
    )
    ON CONFLICT (canonical_taxonomy_node_id, locale)
    DO UPDATE SET
        parent_node_id      = EXCLUDED.parent_node_id,
        node_code           = EXCLUDED.node_code,
        google_category_id  = EXCLUDED.google_category_id,
        depth               = EXCLUDED.depth,
        path                = EXCLUDED.path,
        name                = EXCLUDED.name,
        semantic_text       = EXCLUDED.semantic_text,
        content_hash        = EXCLUDED.content_hash,
        metadata            = EXCLUDED.metadata,
        source_updated_at   = EXCLUDED.source_updated_at,
        is_active           = TRUE,
        updated_at          = NOW()
    RETURNING id INTO v_id;

    RETURN v_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.upsert_segment_embedding_source
(
    p_entity_type VARCHAR,
    p_entity_id BIGINT,
    p_segment_definition_id BIGINT,
    p_segment_code VARCHAR,
    p_name VARCHAR,
    p_semantic_text TEXT,
    p_segment_option_id BIGINT DEFAULT NULL,
    p_option_code VARCHAR DEFAULT NULL,
    p_locale VARCHAR DEFAULT 'pt-BR',
    p_metadata JSONB DEFAULT '{}'::jsonb,
    p_source_updated_at TIMESTAMPTZ DEFAULT NULL
)
RETURNS UUID
LANGUAGE plpgsql
AS $$
DECLARE
    v_content_hash CHAR(64);
    v_id UUID;
BEGIN
    v_content_hash := encode
    (
        digest(convert_to(p_semantic_text, 'UTF8'), 'sha256'),
        'hex'
    );

    INSERT INTO public.segment_embeddings
    (
        entity_type,
        entity_id,
        segment_definition_id,
        segment_option_id,
        segment_code,
        option_code,
        locale,
        name,
        semantic_text,
        content_hash,
        metadata,
        source_updated_at,
        is_active,
        updated_at
    )
    VALUES
    (
        p_entity_type,
        p_entity_id,
        p_segment_definition_id,
        p_segment_option_id,
        p_segment_code,
        p_option_code,
        p_locale,
        p_name,
        p_semantic_text,
        v_content_hash,
        COALESCE(p_metadata, '{}'::jsonb),
        p_source_updated_at,
        TRUE,
        NOW()
    )
    ON CONFLICT (entity_type, entity_id, locale)
    DO UPDATE SET
        segment_definition_id = EXCLUDED.segment_definition_id,
        segment_option_id     = EXCLUDED.segment_option_id,
        segment_code          = EXCLUDED.segment_code,
        option_code           = EXCLUDED.option_code,
        name                  = EXCLUDED.name,
        semantic_text         = EXCLUDED.semantic_text,
        content_hash          = EXCLUDED.content_hash,
        metadata              = EXCLUDED.metadata,
        source_updated_at     = EXCLUDED.source_updated_at,
        is_active             = TRUE,
        updated_at            = NOW()
    RETURNING id INTO v_id;

    RETURN v_id;
END;
$$;

/* ============================================================================
   5. OPTIMISTIC EMBEDDING COMPLETION FUNCTIONS

   p_content_hash must be the hash observed when the embedding request began.
   FALSE means the source changed while the provider call was in flight; the
   generated vector must be discarded and regenerated from the new text.
   ============================================================================ */

CREATE OR REPLACE FUNCTION public.complete_canonical_taxonomy_embedding
(
    p_canonical_taxonomy_node_id BIGINT,
    p_locale VARCHAR,
    p_content_hash CHAR(64),
    p_embedding_provider VARCHAR,
    p_embedding_model VARCHAR,
    p_embedding vector(1536)
)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE public.canonical_taxonomy_embeddings
    SET
        embedding             = p_embedding,
        embedding_provider    = p_embedding_provider,
        embedding_model       = p_embedding_model,
        embedding_dimensions  = 1536,
        embedded_content_hash = p_content_hash,
        embedded_at           = NOW(),
        updated_at            = NOW()
    WHERE canonical_taxonomy_node_id = p_canonical_taxonomy_node_id
      AND locale = p_locale
      AND is_active
      AND content_hash = p_content_hash;

    RETURN FOUND;
END;
$$;

CREATE OR REPLACE FUNCTION public.complete_segment_embedding
(
    p_entity_type VARCHAR,
    p_entity_id BIGINT,
    p_locale VARCHAR,
    p_content_hash CHAR(64),
    p_embedding_provider VARCHAR,
    p_embedding_model VARCHAR,
    p_embedding vector(1536)
)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE public.segment_embeddings
    SET
        embedding             = p_embedding,
        embedding_provider    = p_embedding_provider,
        embedding_model       = p_embedding_model,
        embedding_dimensions  = 1536,
        embedded_content_hash = p_content_hash,
        embedded_at           = NOW(),
        updated_at            = NOW()
    WHERE entity_type = p_entity_type
      AND entity_id = p_entity_id
      AND locale = p_locale
      AND is_active
      AND content_hash = p_content_hash;

    RETURN FOUND;
END;
$$;

/* ============================================================================
   6. COSINE SEARCH FUNCTIONS
   ============================================================================ */

CREATE OR REPLACE FUNCTION public.search_canonical_taxonomy_embeddings
(
    p_query_embedding vector(1536),
    p_locale VARCHAR DEFAULT 'pt-BR',
    p_match_count INTEGER DEFAULT 10,
    p_minimum_similarity DOUBLE PRECISION DEFAULT 0.0
)
RETURNS TABLE
(
    canonical_taxonomy_node_id BIGINT,
    node_code VARCHAR,
    name VARCHAR,
    path TEXT,
    google_category_id BIGINT,
    similarity DOUBLE PRECISION
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        Embedding.canonical_taxonomy_node_id,
        Embedding.node_code,
        Embedding.name,
        Embedding.path,
        Embedding.google_category_id,
        1 - (Embedding.embedding <=> p_query_embedding) AS similarity
    FROM public.canonical_taxonomy_embeddings AS Embedding
    WHERE Embedding.locale = p_locale
      AND Embedding.is_active
      AND Embedding.embedding IS NOT NULL
      AND Embedding.embedded_content_hash = Embedding.content_hash
      AND (1 - (Embedding.embedding <=> p_query_embedding)) >= p_minimum_similarity
    ORDER BY Embedding.embedding <=> p_query_embedding
    LIMIT GREATEST(p_match_count, 1);
$$;

CREATE OR REPLACE FUNCTION public.search_segment_definition_embeddings
(
    p_query_embedding vector(1536),
    p_segment_definition_ids BIGINT[] DEFAULT NULL,
    p_locale VARCHAR DEFAULT 'pt-BR',
    p_match_count INTEGER DEFAULT 10,
    p_minimum_similarity DOUBLE PRECISION DEFAULT 0.0
)
RETURNS TABLE
(
    segment_definition_id BIGINT,
    segment_code VARCHAR,
    name VARCHAR,
    similarity DOUBLE PRECISION
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        Embedding.segment_definition_id,
        Embedding.segment_code,
        Embedding.name,
        1 - (Embedding.embedding <=> p_query_embedding) AS similarity
    FROM public.segment_embeddings AS Embedding
    WHERE Embedding.entity_type = 'SegmentDefinition'
      AND Embedding.locale = p_locale
      AND Embedding.is_active
      AND Embedding.embedding IS NOT NULL
      AND Embedding.embedded_content_hash = Embedding.content_hash
      AND
      (
          p_segment_definition_ids IS NULL
          OR Embedding.segment_definition_id = ANY (p_segment_definition_ids)
      )
      AND (1 - (Embedding.embedding <=> p_query_embedding)) >= p_minimum_similarity
    ORDER BY Embedding.embedding <=> p_query_embedding
    LIMIT GREATEST(p_match_count, 1);
$$;

CREATE OR REPLACE FUNCTION public.search_segment_option_embeddings
(
    p_segment_definition_id BIGINT,
    p_query_embedding vector(1536),
    p_locale VARCHAR DEFAULT 'pt-BR',
    p_match_count INTEGER DEFAULT 10,
    p_minimum_similarity DOUBLE PRECISION DEFAULT 0.0
)
RETURNS TABLE
(
    segment_option_id BIGINT,
    segment_definition_id BIGINT,
    segment_code VARCHAR,
    option_code VARCHAR,
    name VARCHAR,
    similarity DOUBLE PRECISION
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        Embedding.segment_option_id,
        Embedding.segment_definition_id,
        Embedding.segment_code,
        Embedding.option_code,
        Embedding.name,
        1 - (Embedding.embedding <=> p_query_embedding) AS similarity
    FROM public.segment_embeddings AS Embedding
    WHERE Embedding.entity_type = 'SegmentOption'
      AND Embedding.segment_definition_id = p_segment_definition_id
      AND Embedding.locale = p_locale
      AND Embedding.is_active
      AND Embedding.embedding IS NOT NULL
      AND Embedding.embedded_content_hash = Embedding.content_hash
      AND (1 - (Embedding.embedding <=> p_query_embedding)) >= p_minimum_similarity
    ORDER BY Embedding.embedding <=> p_query_embedding
    LIMIT GREATEST(p_match_count, 1);
$$;

COMMIT;

/* ============================================================================
   VERIFICATION QUERIES
   Expected immediately after migration: zero source and pending rows.
   The synchronization API will populate both projections.
   ============================================================================ */

SELECT
    (SELECT COUNT(*) FROM public.canonical_taxonomy_embeddings) AS taxonomy_sources,
    (SELECT COUNT(*) FROM public.pending_canonical_taxonomy_embeddings) AS taxonomy_pending,
    (SELECT COUNT(*) FROM public.segment_embeddings) AS segment_sources,
    (SELECT COUNT(*) FROM public.pending_segment_embeddings) AS segment_pending;

/*
    Retrieval examples after synchronization:

    SELECT *
    FROM public.search_canonical_taxonomy_embeddings
    (
        :query_embedding::vector,
        'pt-BR',
        10,
        0.50
    );

    SELECT *
    FROM public.search_segment_definition_embeddings
    (
        :query_embedding::vector,
        ARRAY[1, 2, 3]::bigint[],
        'pt-BR',
        5,
        0.50
    );

    SELECT *
    FROM public.search_segment_option_embeddings
    (
        1,
        :query_embedding::vector,
        'pt-BR',
        5,
        0.50
    );
*/
