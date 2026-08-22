# ADR-0014: Provider-Neutral Source Taxonomy

- **Status:** Accepted
- **Date:** 2026-08-21
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Catalog Bounded Context — external taxonomy normalization and semantic resolution

## 1. Context

Yunu.Commerce currently implements a Google-specific taxonomy pipeline:

```
Google source
    ↓
GoogleTaxonomy ingestion (Catalog.Application.GoogleTaxonomy, SynchronizeGoogleTaxonomy)
    ↓
SQL Server GoogleTaxonomyCategories (deploy/databases/sqlserver/001-google-taxonomy-tables.sql)
    ↓
Google taxonomy embeddings (PostgreSQL + pgvector, google_taxonomy_embeddings)
    ↓
GoogleCategoryResolver (Catalog.Application.CategoryResolution)
```

This implementation proved the semantic/RAG resolution approach used
elsewhere in Catalog (ADR-0007 — Elasticsearch for search projections;
ADR-0008 — GenAI provider abstraction).

The future catalog architecture must support arbitrary upstream taxonomies
beyond Google, including but not limited to Mercado Livre, Amazon, eBay,
Shopify, Walmart, and customer-specific PIM/ERP/catalog trees, as well as
providers not yet known today. `CanonicalTaxonomyNode`
(`Catalog.Domain.CanonicalTaxonomy`) must not become coupled to any single
provider's model or identifiers, and the future generic resolver must not
contain provider `if` branches (`if Google`, `if MercadoLivre`, `if Amazon`,
...).

## 2. Decision

This ADR freezes a **future, not-yet-implemented** normalization boundary
named `SourceTaxonomy`, planned as an Anti-Corruption Layer (ADR-0001)
between provider-native taxonomy models and Yunu `CanonicalTaxonomy` /
semantic resolution.

The planned architecture is conceptually:

```
Provider-native taxonomy
        ↓
Provider Adapter (e.g. GoogleSourceTaxonomyAdapter — planned)
        ↓
SourceTaxonomy (planned normalized model)
        ↓
generic semantic search / resolver (planned)
        ↓
CanonicalTaxonomy proposal/resolution (planned)
```

`SourceTaxonomy` is **not** merely a rename of `GoogleTaxonomy`.
`GoogleTaxonomy` and `SourceTaxonomy` have different responsibilities (see
§3–§4).

### 2.1 GoogleTaxonomy Remains

`GoogleTaxonomy` remains the provider-specific integration/staging model.
Its existing responsibilities are unchanged by this ADR:

- downloading/importing the Google Product Taxonomy;
- parsing Google-specific identifiers and hierarchy;
- storing Google-native source data (`GoogleTaxonomyCategories`);
- tracking Google imports;
- refreshing the provider-native dataset.

A future `GoogleSourceTaxonomyAdapter` will translate that provider-specific
representation into `SourceTaxonomy`. `GoogleTaxonomy` ingestion is
therefore retained; Google-specific resolver/search layers
(`GoogleCategoryResolver` and related contracts) may eventually be retired,
but only after generic parity is proven (§19). This ADR does not delete
`GoogleTaxonomy`.

### 2.2 Source Taxonomy Responsibility

`SourceTaxonomy` represents a normalized external classification tree. It
answers: *"Which upstream taxonomy/dataset and node does this represent?"*
It does not answer: *"How was this `CanonicalTaxonomyNode` created?"* These
are different concepts:

- **Source taxonomy identity/provenance:** `ProviderCode`, `ScopeCode`,
  `ExternalTaxonomyId`, `ExternalVersion`, `ExternalNodeId`.
- **Canonical provenance:** how a Canonical node was produced/curated (e.g.
  Yunu/manual/AI/governed proposal semantics).

**Update (docs task: "Canonical Taxonomy Provider Decoupling"):** the
provider-identity/provenance coupling described above as technical debt has
been removed. `CanonicalTaxonomyNode` no longer stores `GoogleCategoryId` or
a `CanonicalTaxonomySource` (the enum has been deleted), and
`Catalog.CanonicalTaxonomyNodes` no longer has `GoogleCategoryId` or `Source`
columns (see
`deploy/databases/sqlserver/013-remove-canonical-provider-coupling.sql`).
`CanonicalTaxonomyNode` now represents only approved canonical catalog
truth, with no knowledge of Google or any other upstream provider. This
does not implement `SourceTaxonomy`; provider/source evidence for a
canonical node still belongs to the future `SourceTaxonomy` /
`SourceTaxonomyNode` model described below. Workflow provenance (e.g. AI
proposal + human approval) remains out of scope for this Aggregate and
belongs to future proposal/review metadata. `GoogleTaxonomy` itself
(`Catalog.GoogleTaxonomyCategories`, `GoogleCategoryResolver`) is untouched
by this change and continues to exist as a separate, provider-specific
integration model.

## 3. Normalized Source Taxonomy Header (Planned)


Planned normalized source descriptor table: `Catalog.SourceTaxonomies`.

Conceptual fields:

```
SourceTaxonomyId       BIGINT identity
Code                   NVARCHAR(120), unique
Name                   NVARCHAR(250)
ProviderCode           NVARCHAR(80)
ScopeCode              NVARCHAR(120), nullable
ExternalTaxonomyId     NVARCHAR(200), nullable
ExternalVersion        NVARCHAR(200), nullable
DefaultLanguage        NVARCHAR(10)
SourceUri              NVARCHAR(1000), nullable
SourceChecksum         NVARCHAR(128), nullable
IsActive               BIT
CreatedAt              DATETIME2
UpdatedAt              DATETIME2, nullable
ImportedAt             DATETIME2
```

`ProviderCode` **must** be an extensible string, not an enum: the provider
universe is open-ended. Examples: `google`, `mercadolivre`, `amazon`,
`ebay`, `shopify`, `walmart`, `client`.

`ScopeCode` is generic. Provider-specific columns such as `MarketplaceId`,
`SiteId`, `AmazonMarketplaceId`, or `EbayMarketplaceId` must **not** be
introduced. Examples of `ScopeCode` values: `MLB`, `EBAY_US`, an Amazon
marketplace identifier, or a customer catalog scope.

## 4. Normalized Tree Model (Planned)

Planned normalized tree table: `Catalog.SourceTaxonomyNodes`.

Conceptual fields:

```
SourceTaxonomyNodeId          BIGINT identity
SourceTaxonomyId              BIGINT FK
ExternalNodeId                NVARCHAR(200)
ParentSourceTaxonomyNodeId    BIGINT, nullable FK
NodeType                      NVARCHAR(50)
Name                          NVARCHAR(300)
FullPath                      NVARCHAR(2000)
Level                         INT
IsLeaf                        BIT
IsActive                      BIT
SourceLanguage                NVARCHAR(10)
CreatedAt                     DATETIME2
UpdatedAt                     DATETIME2, nullable
ImportedAt                    DATETIME2
```

Core invariants planned for this table:

- `UNIQUE(SourceTaxonomyId, ExternalNodeId)`.
- FK `SourceTaxonomyId -> SourceTaxonomies`.
- Self FK `ParentSourceTaxonomyNodeId -> SourceTaxonomyNodes`, no cascade
  delete.
- Multiple `ParentSourceTaxonomyNodeId == NULL` roots allowed (see §14).

## 5. Why "Node" Instead of "Category"

`SourceTaxonomyNodes` is intentionally provider-neutral. Not every upstream
classification system is composed only of entities named "Category":

- Google / Mercado Livre / eBay / Shopify: mostly category-like nodes.
- Amazon: `BrowseNode` hierarchy.
- Walmart: can expose `Category`, `ProductTypeGroup`, and `ProductType`
  concepts.

The normalized structure therefore uses `Node` with a `NodeType` column
rather than encoding every provider concept as a `Category`. Example
`NodeType` values: `Category`, `BrowseNode`, `ProductTypeGroup`,
`ProductType`, `Custom`. `NodeType` remains provider-neutral metadata; no
provider-specific table per taxonomy level is planned.

## 6. External Node IDs

`ExternalNodeId` must be a string, never `BIGINT`/`int`: provider identities
are not guaranteed to be numeric or dense. Examples: Google `"187"`, Mercado
Livre `"MLB1055"`; other providers may use opaque or composite identifiers.
Uniqueness is scoped by `(SourceTaxonomyId, ExternalNodeId)` because
different source taxonomies may reuse the same external identifier.

## 7. Parent Resolution

The normalized core model does not persist a second external-parent identity
alongside the internal parent FK. Adapter snapshots may expose
`ParentExternalNodeId` during import; the planned generic synchronizer
resolves hierarchy in two passes:

1. Upsert normalized nodes and resolve internal identities.
2. Resolve `ParentExternalNodeId` → `ParentSourceTaxonomyNodeId`.

Only `ParentSourceTaxonomyNodeId` is persisted as the normalized tree
relationship, avoiding two competing parent-consistency surfaces.

## 8. Source Import History (Planned)

Planned generic import history table:
`Integration.SourceTaxonomyImports`, with fields conceptually including:

```
ImportId
SourceTaxonomyId
AdapterCode
SourceUri
ExternalVersion
SourceChecksum
StartedAt
CompletedAt
NodeCount
InsertedCount
UpdatedCount
DeactivatedCount
Status
ErrorMessage
```

This generalizes behavior conceptually similar to the provider-specific
import tracking already present for `GoogleTaxonomy`.

## 9. Adapter Boundary (Planned)

Planned conceptual provider adapter contract:

```
ISourceTaxonomyAdapter

LoadAsync(
    SourceTaxonomyImportContext context,
    CancellationToken cancellationToken)
```

returning a normalized `SourceTaxonomySnapshot`.

Snapshot descriptor concept: `ProviderCode`, `ScopeCode`,
`ExternalTaxonomyId`, `Version`, `Locale`, `Checksum`.

Snapshot node concept: `ExternalNodeId`, `ParentExternalNodeId`, `NodeType`,
`Name`, `FullPath`, `Level`, `IsLeaf`, `IsActive`.

Adapters translate provider-native data; a generic `SourceTaxonomy`
synchronizer owns persistence. Provider parsing logic must not be placed
inside the generic `SourceTaxonomy` repository.

## 10. Provider Mappings (Illustrative)

To demonstrate the model is intentionally generic:

**Google:** `GoogleCategoryId` → `ExternalNodeId` (string);
`ParentGoogleCategoryId` → parent relationship via adapter/two-pass
resolution; `Name` → `Name`; `FullPath` → `FullPath`; `Level` → `Level`;
`IsLeaf` → `IsLeaf`; `IsActive` → `IsActive`; `SourceLanguage` →
`SourceLanguage`; `NodeType = Category`.

**Mercado Livre:** site/scope such as `MLB` → `ScopeCode`; category ID such
as `MLBxxxx` → `ExternalNodeId`; `path_from_root` → hierarchy/`FullPath`;
`NodeType = Category`.

**Amazon:** marketplace browse tree → one `SourceTaxonomy` scope;
`BrowseNodeId` → `ExternalNodeId`; `NodeType = BrowseNode`. Amazon Product
Type Definition schemas are **not** put into `SourceTaxonomyNodes` (§13).

**eBay:** `categoryTreeId` → `ExternalTaxonomyId`; `categoryTreeVersion` →
`ExternalVersion`; `categoryId` → `ExternalNodeId`.

**Shopify:** taxonomy release → `ExternalVersion`; taxonomy category ID →
`ExternalNodeId`.

**Walmart:** normalize supported hierarchy nodes using `NodeType`:
`Category`, `ProductTypeGroup`, `ProductType`.

**Client:** arbitrary client PIM/ERP/catalog tree → `SourceTaxonomy` through
a client-specific adapter.

## 11. External Attributes Are Separate

Provider category attribute/schema systems are explicitly **not**
normalized into `SourceTaxonomyNodes` v1. Examples: Mercado Livre category
attributes, Amazon Product Type Definitions, eBay item aspects, Shopify
taxonomy attributes, Walmart item specification requirements. No
`ProviderPayloadJson` or other provider-specific attribute JSON blob is
added to `SourceTaxonomyNodes` v1.

A future, separate normalized layer may model external attribute/schema
requirements and map them into Yunu `SegmentDefinitions`, `SegmentOptions`,
`AttributeDefinitions`, `AttributeOptions` — but that is a separate,
not-yet-made architectural decision.

## 12. Source Root Topology

`SourceTaxonomy` must preserve provider topology faithfully; it naturally
supports 1..N roots. `CanonicalTaxonomyRootTopologyPolicy`
(docs/adr/0013-configurable-canonical-taxonomy-root-topology.md) must
**not** be applied to `SourceTaxonomy` storage:

```
upstream taxonomy with multiple roots
    ↓
SourceTaxonomy stores multiple roots faithfully
    ↓
future CanonicalTaxonomyResolve uses customer governance
    ↓
canonical result may follow SingleRoot or MultipleRoots policy
```

The source is evidence/input; the Canonical Taxonomy Root Topology Policy
defines the desired Yunu/customer structure. These are not the same
concept.

## 13. Persistence Boundary

SQL Server is planned as the `SourceTaxonomy` source of truth.
PostgreSQL/pgvector is a rebuildable semantic projection, matching the
mature Catalog semantic architecture already used for Google Taxonomy
embeddings and Segment embeddings. pgvector is not made authoritative.

## 14. Future pgvector Projection (Planned)

Planned future projection: `public.source_taxonomy_embeddings`, with
metadata conceptually including:

```
source_taxonomy_id
source_taxonomy_node_id
external_node_id
node_type
level
path
locale
name
semantic_text

embedding
embedding_provider
embedding_model
embedding_dimensions

content_hash
embedded_content_hash
source_updated_at
embedded_at
is_active
created_at
updated_at
```

The mature projection pattern is expected to include: deterministic semantic
text; content hash; embedded content hash; stale-vector detection; an active
flag; and a partial HNSW index restricted to valid/current vectors. This ADR
does not implement this schema.

## 15. Source-Scoped Semantic Search

This is a critical planned invariant: normal semantic resolution must always
be scoped to exactly one `SourceTaxonomy`:

```
Resolve(
    sourceTaxonomyId,
    semanticQuery)
```

An accidental global vector search across Google, Mercado Livre, Amazon,
client catalogs, etc. must not occur; otherwise candidates from unrelated
taxonomies could leak into one resolution. Multi-source resolution may be
added later only as an explicit orchestration capability — it must never
happen accidentally merely because the vector table contains multiple
sources.

## 16. Generic Resolver (Planned)

The future resolver is planned to be provider-neutral: `SourceTaxonomyResolver`,
with generic contracts such as `ISourceTaxonomySemanticSearch` and
`ISourceTaxonomyCatalogReader`, and generic candidate/result models. The
initial implementation is expected to port the proven behavior of
`GoogleCategoryResolver` — vector retrieval, thresholds, candidate handling,
reranking behavior, confidence behavior — without redesigning the algorithm
during the port. Parity is proven first; the algorithm evolves afterward.

## 17. Google Parity Gate

Before retiring `GoogleCategoryResolver`, both `GoogleCategoryResolver` and
`SourceTaxonomyResolver(Source = normalized Google taxonomy)` must be run
against the same regression corpus, comparing: top-1; top-K; confidence;
reranking behavior; acceptance/rejection; and known catalog resolution
scenarios. No meaningful unexplained regression is accepted. Only after
parity is demonstrated and runtime consumers are migrated may
Google-specific semantic resolver contracts be removed.

## 18. What Is Retired vs. Retained

Eventually **retired**, after parity (§17):

- `GoogleCategoryResolver`;
- obsolete Google-specific semantic search abstractions;
- obsolete Google-specific category-resolution endpoint/contracts;
- the old Google-specific pgvector projection, after a separate rollback
  window.

**Retained:**

- Google taxonomy synchronization/import (`GoogleTaxonomy` ingestion);
- the Google taxonomy provider-native SQL model
  (`GoogleTaxonomyCategories`);
- Google import tracking, where still needed;
- the planned `GoogleSourceTaxonomyAdapter`.

Removing the Google-specific resolver is not the same as removing Google as
an upstream provider: Google remains a first-class `SourceTaxonomy`
provider.

## 19. Canonical Taxonomy Resolution (Future)

Planned future high-level handoff:

```
SourceTaxonomyResolver
        ↓
SourceTaxonomyNode candidate
        ↓
CanonicalTaxonomyResolve
        ↓
consider:
    existing CanonicalTaxonomy
    RootTopologyPolicy (ADR-0013)
    customer AuditProfile
    EffectiveSegmentDefinitions
    Attributes / Segments
        ↓
proposal
        ↓
human governance where required
```

`CanonicalTaxonomyResolve` must not branch on provider (`if Google`,
`if MercadoLivre`, `if Amazon`, ...). Provider-specific semantics must
terminate at adapters / `SourceTaxonomy` normalization.

## 20. Implementation Sequence (Planned, Not Implemented by This ADR)

The intended staged implementation sequence, at a high level:

1. SQL Server `SourceTaxonomy` schema.
2. `SourceTaxonomy` contracts and SQL persistence/readers.
3. Adapter contract + generic import orchestration.
4. `GoogleSourceTaxonomyAdapter`.
5. Prove the normalized schema with one non-Google provider, preferably
   Mercado Livre.
6. Freeze `SourceTaxonomy` v1 schema.
7. Add the mature pgvector `SourceTaxonomy` projection.
8. Add `SourceTaxonomy` embedding synchronization.
9. Add generic semantic search contracts.
10. Implement `SourceTaxonomyResolver`.
11. Run the Google resolver parity gate (§17).
12. Migrate orchestrator/runtime consumers.

## 21. Implementation Status

Implemented:

- Phase 1 SQL Server foundation (§3-§4, §8;
  `deploy/databases/sqlserver/014-create-source-taxonomy-foundation.sql`);
- Phase 2 Application contracts + SQL persistence/readers
  (`Application.SourceTaxonomy`, `SqlSourceTaxonomyRepository`);
- Phase 3 provider-neutral adapter contract + generic import orchestration
  (`Application.SourceTaxonomy.Import`: `ISourceTaxonomyAdapter`,
  `SourceTaxonomyImportContext`, `SourceTaxonomySnapshot`,
  `SourceTaxonomySnapshotValidator`, `SourceTaxonomyImportOrchestrator`,
  `ISourceTaxonomyImportStore`, `ISourceTaxonomySynchronizationStore`,
  `ISourceTaxonomyImportGuard`; SQL Server implementations:
  `SqlSourceTaxonomyImportStore`, `SqlSourceTaxonomySynchronizationStore`,
  `InMemorySourceTaxonomyImportGuard`). Two-pass hierarchy resolution (§7,
  §14), checksum-based unchanged-snapshot skip (§16), source-identity safety
  checks (§8) and Started/Completed/Failed import history lifecycle (§8,
  §11-§12) are implemented and covered by real SQL Server integration tests.
  No concrete provider adapter exists yet; production adapter registration is
  intentionally empty.

Still pending:

- concrete Google adapter (`GoogleSourceTaxonomyAdapter`);
- non-Google proof (e.g. Mercado Livre);
- schema freeze;
- pgvector `SourceTaxonomy` projection;
- `SourceTaxonomy` embedding synchronization;
- generic semantic search / `SourceTaxonomyResolver`;
- Google resolver parity gate and consumer migration (§17);
- `CanonicalTaxonomyResolve` (§19).
13. Retire the obsolete Google-specific resolver layer, only after parity.

This sequence is architectural guidance. ADR-0014 does not implement any
step of it.

## 21. Consequences

**Positive:**

- Provider-neutral catalog resolution.
- `CanonicalTaxonomy` isolated from provider APIs/models.
- The Google implementation becomes one adapter instead of being the
  architecture itself.
- Future providers can be added without changing `CanonicalTaxonomy`.
- Generic RAG infrastructure (proven via Google Taxonomy and Segment
  embeddings) can be reused.
- Customer-specific catalog trees can participate through the same adapter
  boundary.
- Provider-native data remains traceable through retained `GoogleTaxonomy`
  ingestion (and future equivalents).
- SQL Server remains authoritative while the semantic projection stays
  rebuildable.

**Trade-offs:**

- Introduces an additional normalization layer.
- Temporary coexistence of Google-specific and generic pipelines during
  migration.
- Parity validation is required before removing legacy resolver components.
- Adapters must preserve source semantics accurately.
- External attribute systems need a separate future normalization model
  (§11).

## 22. Explicitly Not Changed

This ADR does **not**:

- implement `SourceTaxonomy` tables;
- implement adapters;
- implement Mercado Livre integration;
- implement Amazon integration;
- implement pgvector tables for `SourceTaxonomy`;
- generate embeddings;
- implement `SourceTaxonomyResolver`;
- change `GoogleTaxonomy` ingestion;
- remove `GoogleCategoryResolver`;
- remove `google_taxonomy_embeddings`;
- change `CanonicalTaxonomySource`;
- implement `CanonicalTaxonomyResolve`;
- implement `ProductProposal` materialization;
- change `SegmentDefinitions`;
- change `SegmentOptions`;
- change `Product`/`Sku`;
- add public mutation APIs.

## 23. Related Decisions

- docs/adr/0001-use-ddd-clean-hexagonal.md
- docs/adr/0007-use-elasticsearch-for-search-projections.md
- docs/adr/0008-genai-provider-abstraction.md
- docs/adr/0011-no-public-crud-for-structural-catalog-data.md
- docs/adr/0013-configurable-canonical-taxonomy-root-topology.md
