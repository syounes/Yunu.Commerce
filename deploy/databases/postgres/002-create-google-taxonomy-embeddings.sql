-- Idempotent creation of the Google Taxonomy embeddings table (pgvector).
-- google_category_id + provider + model uniquely identifies an embedding,
-- allowing the same category to have distinct embeddings across different
-- providers/models (multi-provider AI architecture) without overwriting them.
CREATE TABLE IF NOT EXISTS google_taxonomy_embeddings (
    id                 uuid PRIMARY KEY,
    google_category_id integer NOT NULL,
    category_path      text NOT NULL,
    provider           text NOT NULL,
    model              text NOT NULL,
    dimensions         integer NOT NULL,
    embedding          vector(1536) NOT NULL,
    created_at_utc     timestamptz NOT NULL,
    updated_at_utc     timestamptz NOT NULL,
    CONSTRAINT uq_google_taxonomy_embeddings_category_provider_model
        UNIQUE (google_category_id, provider, model)
);
