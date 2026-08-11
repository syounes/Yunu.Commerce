# ADR-0007: Use Elasticsearch for Search Projections

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Product search, denormalized commerce projections, full-text search and future vector/semantic retrieval

## 1. Context

Yunu.Commerce owns commerce data across independent Bounded Contexts:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

Customer-facing product discovery requires a read model that combines information from several of these contexts.

A typical searchable commerce document may require:

```text
Product
SKU
Brand
Category
Attributes
Seller
Offer
Current Price
PIX Price
Boleto Price
Regional Price
Availability
Regional Availability
Fulfillment information
Searchable text
```

Querying every owning database synchronously for every search request would create excessive coupling, latency and operational complexity.

The platform also requires future support for:

```text
full-text search
filters
facets
ranking
autocomplete
semantic retrieval
vector search
GenAI/RAG scenarios
```

## 2. Decision

Yunu.Commerce will use Elasticsearch as the primary Search projection store.

Elasticsearch will contain denormalized, query-optimized documents built from events emitted by canonical Bounded Contexts.

Conceptually:

```text
Catalog ────────┐
Sellers ────────┤
Offers ─────────┤
Pricing ────────┤
Availability ───┤
Fulfillment ────┤
                ▼
              Kafka
                │
                ▼
       Search Projection Worker
                │
                ▼
          Elasticsearch
                │
                ▼
           Search API
```

## 3. Elasticsearch Is Not Canonical Business Storage

The fundamental rule is:

> Elasticsearch is a derived read model, not the source of truth for commerce business state.

Canonical ownership remains with the appropriate Bounded Context.

Example:

```text
Catalog
→ Product truth

Pricing
→ Price truth

Availability
→ Availability truth

Elasticsearch
→ searchable projection of those truths
```

## 4. Why Elasticsearch

Elasticsearch is selected because Yunu.Commerce requires:

```text
full-text search
high-performance filtering
faceting
sorting
relevance scoring
denormalized documents
high-volume reads
index aliases
bulk indexing
future vector search
```

These capabilities fit product discovery better than forcing transactional databases to behave as search engines.

## 5. Search Projection

Search documents are intentionally denormalized.

A conceptual document may look like:

```text
CommerceProductDocument
│
├── ProductId
├── ProductName
├── Description
├── Brand
├── Category
├── Attributes
├── SKUs
│   ├── SkuId
│   ├── Seller/Offer summaries
│   ├── Price summaries
│   └── Availability summaries
├── SearchText
├── Embedding
└── ProjectionVersion
```

The exact shape will evolve from real query requirements.

## 6. Projection Ownership

Search owns the Elasticsearch projection model.

It does not own:

```text
Product
Seller
Offer
Price
Availability
```

Those remain owned by their source contexts.

## 7. No Domain Dependency on Elasticsearch

Forbidden:

```text
Catalog.Domain
     │
     ▼
Elasticsearch Client
```

Correct:

```text
Search Application / Worker
        │
        ▼
Search Index Port
        │
        ▼
Elasticsearch Adapter
```

Provider-specific clients remain in Infrastructure.

## 8. Search API

Search queries should use a provider-neutral Search abstraction.

Conceptually:

```text
Search API
    │
    ▼
Search Application
    │
    ▼
IProductSearch
    │
    ▼
ElasticsearchProductSearch
```

Application code should express search intent, not Elasticsearch query DSL details.

## 9. Event-Driven Projection Updates

Search projections will primarily be updated asynchronously from Kafka Integration Events.

Examples:

```text
ProductCreated
ProductUpdated
SkuCreated
SkuUpdated
SellerUpdated
OfferActivated
OfferUpdated
PriceChanged
AvailabilityChanged
FulfillmentNodeChanged
```

## 10. Reliable Event Production

Events feeding Search must originate through the Transactional Outbox strategy.

Conceptually:

```text
Business Context
      │
      ▼
Canonical Database + Outbox
      │
      ▼
Kafka
      │
      ▼
Search Projection
```

This prevents Search from depending on unsafe database/Kafka dual writes.

## 11. Eventual Consistency

Search is eventually consistent with canonical contexts.

Example:

```text
Price changed at T0
      │
      ▼
Pricing committed
      │
      ▼
PriceChanged published
      │
      ▼
Search updated at T0 + Δ
```

The projection delay must be measured.

## 12. Projection Lag

Search telemetry must expose projection lag.

Important measurements:

```text
event occurred time
event consumed time
document indexed time
end-to-end projection latency
consumer lag
```

## 13. Idempotency

Search consumers must be idempotent because Kafka delivery is at least once.

Repeated processing of the same event must not corrupt the index.

Potential mechanisms:

```text
EventId
source entity version
projection version
upsert semantics
Inbox where required
```

## 14. Out-of-Order Events

Consumers must consider out-of-order delivery across independent streams.

Where necessary, source versions should prevent an older event from overwriting newer projected state.

Example:

```text
PriceChanged version 12
arrives before
PriceChanged version 11
```

Version 11 must not revert the projection.

## 15. Index Strategy

Indices should represent stable Search use cases rather than mirror database tables.

Avoid:

```text
products-index
prices-index
availability-index
```

when every customer search would then require application-side joins.

Prefer query-oriented projections.

## 16. Initial Product Index

Initial conceptual index:

```text
yunu-commerce-products-v1
```

A stable alias should hide the physical version:

```text
yunu-commerce-products
        │
        ▼
yunu-commerce-products-v1
```

Applications query the alias.

## 17. Index Aliases

Aliases allow zero/minimal-downtime index migrations.

Example:

```text
yunu-commerce-products
        │
        ▼
products-v1
```

Rebuild:

```text
products-v2
```

After validation:

```text
yunu-commerce-products
        │
        ▼
products-v2
```

The old index can then be retained temporarily or removed.

## 18. Index Versioning

Breaking mapping changes require a new physical index version.

Do not attempt dangerous in-place mapping mutations when a rebuild is safer.

## 19. Rebuildability

Search projections must be rebuildable.

Potential rebuild sources:

```text
canonical context APIs/data exports
Kafka replay
snapshot + event catch-up
dedicated projection bootstrap
```

Elasticsearch loss must not imply loss of canonical commerce truth.

## 20. Rebuild Process

Conceptually:

```text
Create new index
      │
      ▼
Load canonical snapshot
      │
      ▼
Catch up events
      │
      ▼
Validate
      │
      ▼
Switch alias
      │
      ▼
Retire old index
```

## 21. Full-Text Search

Elasticsearch will support textual search over fields such as:

```text
Product name
Description
Brand
Category
Search keywords
Selected attributes
```

Analyzer choices must consider Portuguese initially and future multilingual commerce requirements.

## 22. Filters

Structured filters may include:

```text
Category
Brand
Seller
1P / 3P
Price range
Availability
Region
Fulfillment type
Attributes
```

Fields used for filtering should have suitable mappings.

## 23. Facets

Search may expose aggregations/facets such as:

```text
Brands
Categories
Price ranges
Seller types
Attributes
Availability
```

Facet design must be based on actual product-discovery requirements.

## 24. Sorting

Potential sort options:

```text
relevance
price ascending
price descending
newest
popularity
```

Popularity/ranking signals require separate business definitions and are not invented by Search.

## 25. Regional Search

Search may require region-sensitive projections.

Example:

```text
Customer Region
      │
      ▼
Search
      │
      ├── Regional price
      └── Regional availability
```

The index design must avoid uncontrolled document explosion.

Regional modeling will be validated against expected number of regions, SKUs and update frequency.

## 26. Availability Update Volume

Availability can generate significantly more updates than Product data.

Search should not blindly index every inventory quantity fluctuation if the search experience only requires:

```text
available / unavailable
```

or a coarse availability state.

Projection events should be semantically optimized.

## 27. Search vs Availability API

Elasticsearch may answer:

```text
Is this item broadly searchable/available?
```

A dedicated Availability capability may still answer:

```text
What is the precise current sellable quantity?
```

Search projections do not automatically replace operational availability queries.

## 28. Search vs Pricing API

Search may contain the current projected price for discovery pages.

For workflows requiring authoritative financial freshness, Pricing remains the owning capability.

## 29. Bulk Indexing

Projection workers should use Elasticsearch bulk operations where appropriate.

Benefits include:

```text
higher throughput
lower network overhead
better indexing efficiency
```

Batch sizes and concurrency must be bounded and measured.

## 30. Backpressure

Search consumers must protect Elasticsearch during high event volume.

Potential mechanisms:

```text
bounded batches
bounded concurrency
Kafka pause/resume
retry with backoff
bulk indexing
```

## 31. Retry

Transient Elasticsearch failures may be retried.

Examples:

```text
temporary timeout
node unavailable
temporary network failure
```

Permanent mapping/data failures should not retry indefinitely.

## 32. Dead-Letter Handling

Projection messages that cannot be processed after the configured policy may be isolated for investigation and replay.

A failed document must not silently disappear.

## 33. Mapping

Mappings must be explicit for important fields.

Avoid relying entirely on dynamic mapping for core commerce fields.

Explicit mappings improve:

```text
correctness
storage efficiency
query behavior
migration safety
```

## 34. Dynamic Product Attributes

Catalog products may contain category-specific dynamic attributes.

Search mapping must balance:

```text
flexibility
mapping explosion risk
filter requirements
index size
```

The platform must not create uncontrolled unique fields for arbitrary attribute names.

## 35. Mapping Explosion Protection

Dynamic attributes may require normalized structures such as:

```text
attribute key
attribute value
attribute type
```

or other controlled strategies.

Exact design will be validated during Catalog implementation.

## 36. Document Size

Search documents must not grow without bounds.

Large media payloads, binary content and unnecessary historical data do not belong inside product Search documents.

Store references/URLs instead.

## 37. Images and Media

Elasticsearch may index media metadata required for search responses.

Actual binary assets belong in Object Storage/CDN infrastructure.

## 38. Search Result DTO

Search returns dedicated read DTOs.

It must not deserialize Elasticsearch documents into Domain Aggregates.

Example:

```text
ProductSearchResult
SkuSearchResult
OfferSearchSummary
```

## 39. Pagination

Deep offset pagination should be avoided for large result sets.

Appropriate Elasticsearch pagination mechanisms should be used according to the use case.

The provider-specific implementation remains inside the adapter.

## 40. Semantic Search

Yunu.Commerce intends to support future semantic product search.

Example:

```text
"quero um notebook leve para viajar e programar .NET"
```

This may use embeddings to retrieve semantically relevant products beyond keyword matching.

## 41. Vector Search

Elasticsearch may store vector embeddings alongside product Search documents or in a dedicated vector-oriented index depending on measured requirements.

Conceptually:

```text
Product content
      │
      ▼
Embedding Generator
      │
      ▼
Vector
      │
      ▼
Elasticsearch
```

## 42. AI Provider Independence

Embedding generation must use a provider-neutral abstraction.

Conceptually:

```text
IEmbeddingGenerator
      │
      ├── Azure AI Adapter
      └── Google AI Adapter
```

Search must not become coupled to one AI provider.

See ADR-0008.

## 43. Embedding Lifecycle

Embeddings should be regenerated when semantically relevant source content changes.

Examples:

```text
Product name changed
Description changed
Brand/category changed
Important attributes changed
```

A simple price change should not necessarily regenerate the product text embedding.

## 44. Embedding Event Flow

Potential future flow:

```text
ProductUpdated
      │
      ▼
Kafka
      │
      ▼
Embedding Worker
      │
      ▼
IEmbeddingGenerator
      │
      ▼
Vector Update
      │
      ▼
Elasticsearch
```

## 45. Hybrid Search

Future Search may combine:

```text
lexical relevance
+
vector similarity
+
business filters
+
commercial ranking
```

This is known as hybrid retrieval.

The exact ranking strategy will be developed after baseline lexical Search exists.

## 46. RAG

Elasticsearch may participate in Retrieval-Augmented Generation.

Potential flow:

```text
User request
    │
    ▼
Embedding
    │
    ▼
Elasticsearch retrieval
    │
    ▼
Relevant product context
    │
    ▼
Generative AI
    │
    ▼
Commerce assistant response
```

RAG retrieval does not grant AI authority to modify canonical commerce data.

## 47. Search Security

Search documents must not expose data the customer/application is not authorized to see.

Do not index secrets or internal operational information merely because Elasticsearch can store it.

## 48. Multi-Tenancy

If Yunu.Commerce later becomes multi-tenant, index isolation strategy must be explicitly designed.

Potential approaches include:

```text
tenant field
index per tenant group
index per tenant
```

No strategy is selected until actual tenancy requirements are defined.

## 49. Observability

Elasticsearch telemetry should include:

```text
search latency
indexing latency
bulk failures
indexing throughput
document count
index size
cluster health
projection lag
query error rate
```

## 50. Search Analytics

Future search analytics may capture:

```text
queries
zero-result searches
click-through
conversion
ranking effectiveness
```

Analytics must be designed separately from canonical Search indexing.

## 51. OpenTelemetry

Search APIs and projection workers should participate in distributed tracing.

Important trace boundaries:

```text
Kafka consume
projection processing
Elasticsearch bulk operation
Search API query
```

## 52. Timeouts

Every Elasticsearch operation must use bounded timeouts.

Search infrastructure must not block requests indefinitely.

## 53. Resilience

Retry and circuit-breaking strategies may be used where appropriate.

Retries must be bounded and should not amplify a failing cluster.

## 54. Local Development

Elasticsearch should be available in local Docker infrastructure.

Conceptually:

```text
docker compose
│
├── MongoDB
├── Relational DB
├── Kafka
├── Redis
└── Elasticsearch
```

Developers must be able to run the Search pipeline without requiring cloud infrastructure.

## 55. Testing

Search testing should include:

```text
index creation
mapping
product indexing
updates
idempotent replay
out-of-order protection
full-text queries
filters
facets
sorting
alias switching
rebuild
bulk indexing
failure handling
```

## 56. Integration Tests

Use real Elasticsearch through Testcontainers where practical.

Do not mock away important Search behavior such as analyzers, mappings and query semantics.

## 57. Architecture Tests

Architecture tests should verify:

```text
Domain projects do not reference Elasticsearch packages.

Application does not contain Elasticsearch DSL.

Search Infrastructure implements provider-neutral ports.

Commerce Domains do not depend on Search projection models.

Search does not become canonical business ownership.
```

## 58. Initial Implementation Sequence

Recommended order:

```text
1. Add Elasticsearch to Docker Compose

2. Define Search module/project boundaries

3. Define Product Search projection contract

4. Define index mapping

5. Implement Elasticsearch adapter

6. Implement Search Projection Worker

7. Consume ProductCreated

8. Consume ProductUpdated

9. Add Product Search API

10. Add filters and facets

11. Add PriceChanged projection

12. Add AvailabilityChanged projection

13. Implement index aliases/rebuild

14. Add observability

15. Add embeddings/vector search later
```

## 59. First Search Vertical Slice

The first complete Search flow should be:

```text
Create Product
      │
      ▼
Catalog Domain
      │
      ▼
MongoDB + Outbox
      │
      ▼
Kafka
      │
      ▼
Search Projection Worker
      │
      ▼
Elasticsearch
      │
      ▼
Search API
      │
      ▼
Product result
```

This is one of the first end-to-end architecture proofs for Yunu.Commerce.

## 60. Second Search Slice

After Catalog Search works:

```text
PriceChanged
      │
      ▼
Kafka
      │
      ▼
Search Projection
      │
      ▼
Projected current price
```

## 61. Third Search Slice

Then:

```text
AvailabilityChanged
      │
      ▼
Kafka
      │
      ▼
Search Projection
      │
      ▼
Searchable availability
```

## 62. Fourth Search Slice

Then introduce:

```text
Product content
      │
      ▼
Embedding Worker
      │
      ▼
Azure AI or Google AI
      │
      ▼
Vector
      │
      ▼
Elasticsearch
      │
      ▼
Semantic Search
```

## 63. Consequences

### Positive

```text
fast product discovery
full-text search
filters and facets
denormalized commerce views
independent scaling
rebuildable read model
reduced cross-context runtime coupling
future vector search
future RAG support
```

### Negative

```text
eventual consistency
additional infrastructure
projection complexity
index mapping management
rebuild procedures
duplicate data
operational monitoring
high-volume update considerations
```

These tradeoffs are accepted.

## 64. Alternatives Considered

### Query Canonical Databases Directly

Rejected for product discovery because it would require expensive cross-context composition and would provide weaker search capabilities.

### Relational Full-Text Search as Primary Platform Search

Not selected because Elasticsearch better matches the expected filtering, faceting, denormalization and future vector-search requirements.

### Redis as Search Engine

Rejected because Redis serves different low-latency cache/projection concerns.

### Elasticsearch as Canonical Catalog Database

Rejected because Search indexing requirements must not define Catalog's Domain ownership or persistence model.

### Dedicated Vector Database Immediately

Not selected initially.

Elasticsearch vector capabilities allow the first semantic-search architecture to remain simpler.

A dedicated vector store may be evaluated later if scale, quality or cost requires it.

## 65. Copilot Rules

GitHub Copilot must:

```text
Never reference Elasticsearch packages from Domain.

Never treat Elasticsearch as canonical commerce storage.

Never deserialize Search documents into Domain Aggregates.

Keep Elasticsearch DSL inside Infrastructure.

Use provider-neutral Search ports.

Build Search from explicit projections.

Consume reliable Integration Events.

Assume eventual consistency.

Make projection consumers idempotent.

Protect against stale/out-of-order updates where required.

Use index aliases for breaking mapping migrations.

Make projections rebuildable.

Use bulk indexing for appropriate high-volume workloads.

Avoid mapping explosion.

Do not store binary media in Elasticsearch.

Add projection-lag telemetry.

Keep embedding generation behind AI provider abstractions.

Do not allow AI/Search projections to bypass owning Domain rules.
```

## 66. Relationship to Other ADRs

This ADR depends on:

```text
ADR-0001
Use DDD, Clean Architecture and Hexagonal Architecture

ADR-0002
Bounded Context Strategy

ADR-0003
Database per Bounded Context

ADR-0004
Use Kafka for Event-Driven Integration

ADR-0005
Use Transactional Outbox
```

It complements:

```text
ADR-0006
Use Redis for Distributed Cache

ADR-0008
GenAI Provider Abstraction

ADR-0009
Cloud Provider Strategy
```

## 67. Final Decision

Yunu.Commerce adopts Elasticsearch as its primary product Search and denormalized Search-projection platform.

Canonical Bounded Contexts remain the owners of business truth.

Kafka Integration Events continuously build and update Elasticsearch read models.

The same Search platform may later support embeddings, vector retrieval, hybrid search and RAG without coupling the Domain to a specific AI provider.

The defining principle is:

> Canonical contexts own commerce truth. Elasticsearch owns the optimized view that makes that truth searchable.
