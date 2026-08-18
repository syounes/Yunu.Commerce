/*
    Yunu.Commerce - Segment assignment scope vector projection
    Target: PostgreSQL 17 + pgvector 0.8+

    SQL Server remains the source of truth. This column is copied to pgvector
    so retrieval can discard definitions that cannot be assigned to the target
    aggregate before producing Top-K candidates.
*/

BEGIN;

DO $$
BEGIN
    IF TO_REGCLASS('public.segment_embeddings') IS NULL THEN
        RAISE EXCEPTION 'public.segment_embeddings does not exist';
    END IF;
END;
$$;

ALTER TABLE public.segment_embeddings
    ADD COLUMN IF NOT EXISTS assignment_scope VARCHAR(32);

UPDATE public.segment_embeddings
SET assignment_scope = CASE
    WHEN segment_code IN ('target_audience', 'gender')
        THEN 'ProductWithSkuOverride'
    WHEN segment_code IN
    (
        'sport_modality',
        'foot_pronation',
        'computer_profile'
    )
        THEN 'Product'
    ELSE COALESCE(assignment_scope, 'Product')
END
WHERE assignment_scope IS NULL
   OR assignment_scope <> CASE
        WHEN segment_code IN ('target_audience', 'gender')
            THEN 'ProductWithSkuOverride'
        WHEN segment_code IN
        (
            'sport_modality',
            'foot_pronation',
            'computer_profile'
        )
            THEN 'Product'
        ELSE assignment_scope
      END;

ALTER TABLE public.segment_embeddings
    ALTER COLUMN assignment_scope SET DEFAULT 'Product',
    ALTER COLUMN assignment_scope SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'public.segment_embeddings'::regclass
          AND conname = 'ck_segment_embeddings_assignment_scope'
    ) THEN
        ALTER TABLE public.segment_embeddings
            ADD CONSTRAINT ck_segment_embeddings_assignment_scope
            CHECK
            (
                assignment_scope IN
                (
                    'Product',
                    'Sku',
                    'ProductWithSkuOverride'
                )
            );
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS ix_segment_embeddings_scope_type_locale
    ON public.segment_embeddings
    (
        assignment_scope,
        entity_type,
        locale,
        is_active
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
    source_updated_at,
    assignment_scope
FROM public.segment_embeddings
WHERE is_active
  AND
  (
      embedding IS NULL
      OR embedded_content_hash IS DISTINCT FROM content_hash
  );

/* Remove the pre-scope overload created by migration 004. */
DROP FUNCTION IF EXISTS public.upsert_segment_embedding_source
(
    VARCHAR,
    BIGINT,
    BIGINT,
    VARCHAR,
    VARCHAR,
    TEXT,
    BIGINT,
    VARCHAR,
    VARCHAR,
    JSONB,
    TIMESTAMPTZ
);

CREATE OR REPLACE FUNCTION public.upsert_segment_embedding_source
(
    p_entity_type VARCHAR,
    p_entity_id BIGINT,
    p_segment_definition_id BIGINT,
    p_segment_code VARCHAR,
    p_name VARCHAR,
    p_semantic_text TEXT,
    p_assignment_scope VARCHAR,
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
        assignment_scope,
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
        p_assignment_scope,
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
        assignment_scope      = EXCLUDED.assignment_scope,
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

/* Remove the pre-scope search overload created by migration 004. */
DROP FUNCTION IF EXISTS public.search_segment_definition_embeddings
(
    vector,
    BIGINT[],
    VARCHAR,
    INTEGER,
    DOUBLE PRECISION
);

CREATE OR REPLACE FUNCTION public.search_segment_definition_embeddings
(
    p_query_embedding vector(1536),
    p_target_scope VARCHAR DEFAULT NULL,
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
    assignment_scope VARCHAR,
    similarity DOUBLE PRECISION
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        Embedding.segment_definition_id,
        Embedding.segment_code,
        Embedding.name,
        Embedding.assignment_scope,
        1 - (Embedding.embedding <=> p_query_embedding) AS similarity
    FROM public.segment_embeddings AS Embedding
    WHERE Embedding.entity_type = 'SegmentDefinition'
      AND Embedding.locale = p_locale
      AND Embedding.is_active
      AND Embedding.embedding IS NOT NULL
      AND Embedding.embedded_content_hash = Embedding.content_hash
      AND
      (
          p_target_scope IS NULL
          OR Embedding.assignment_scope = p_target_scope
          OR Embedding.assignment_scope = 'ProductWithSkuOverride'
      )
      AND
      (
          p_segment_definition_ids IS NULL
          OR Embedding.segment_definition_id = ANY (p_segment_definition_ids)
      )
      AND (1 - (Embedding.embedding <=> p_query_embedding)) >= p_minimum_similarity
    ORDER BY Embedding.embedding <=> p_query_embedding
    LIMIT GREATEST(p_match_count, 1);
$$;

COMMIT;

SELECT
    assignment_scope,
    entity_type,
    COUNT(*) AS record_count
FROM public.segment_embeddings
GROUP BY assignment_scope, entity_type
ORDER BY assignment_scope, entity_type;
