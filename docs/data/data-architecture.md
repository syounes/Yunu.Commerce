# Yunu.Commerce - Data Architecture

## 1. Purpose

This document defines the Data Architecture for Yunu.Commerce.

The objective is to define how data is owned, persisted, queried, cached, indexed, projected and evolved across Bounded Contexts.

Yunu.Commerce uses Polyglot Persistence.

No single database technology is expected to solve every problem.

Technology selection must follow:

- Domain semantics
- Consistency requirements
- Access patterns
- Data volume
- Write frequency
- Read frequency
- Query flexibility
- Operational requirements
- Scalability
- Recovery requirements

The central rule is:

> Each Bounded Context owns its data. Storage technology is an implementation detail.

---

# 2. Data Architecture Principles

The initial principles are:

```text
Each Bounded Context owns its data.

No Bounded Context may directly update another context's data.

No database technology may dictate Domain modeling.

Canonical data and read projections are different concerns.

Caches are not sources of truth.

Search indexes are not sources of truth.

Cross-context consistency is generally eventual.

Relational and non-relational stores may coexist.

Infrastructure must remain replaceable where practical.
```

---

# 3. Polyglot Persistence

The initial technology candidates are:

```text
Relational Database
    SQL Server
    PostgreSQL
    Azure SQL

Document Database
    MongoDB

Distributed Cache
    Redis

Search Engine
    Elasticsearch

Vector Search
    Elasticsearch vector capabilities
    or a replaceable vector store

Object Storage
    Azure Blob Storage
    Google Cloud Storage
    or another provider

Event Streaming
    Kafka
```

Each technology has a defined role.

---

# 4. Data Ownership

Every Bounded Context has logical ownership of its data.

Conceptually:

```text
Catalog
    owns Catalog data

Sellers
    owns Seller data

Offers
    owns Offer data

Pricing
    owns Pricing data

Availability
    owns Availability data

Fulfillment
    owns Fulfillment data

Freight
    owns Freight-specific persisted data

Search
    owns Search projections

AI
    owns AI operational and enrichment data
```

Logical ownership must remain explicit even when physical infrastructure is shared.

---

# 5. Physical Infrastructure vs Logical Ownership

Multiple contexts may initially share the same database server or cluster.

Example:

```text
SQL Server

├── sellers
├── pricing
└── fulfillment
```

or:

```text
MongoDB Cluster

├── catalog
├── availability
└── ai
```

This is acceptable if:

- schema/database ownership remains explicit
- one context does not directly mutate another context's data
- migrations remain independently controlled
- access credentials and permissions can evolve independently
- future extraction remains possible

---

# 6. No Cross-Context Database Access

Forbidden:

```text
Pricing
   │
   ▼
SELECT *
FROM Catalog.Products
```

Forbidden:

```text
Availability
   │
   ▼
direct update of FulfillmentNode table
```

Preferred:

```text
Integration Event

Application API

Projection

Explicit Contract
```

Database boundaries must reinforce Domain boundaries.

---

# 7. Source of Truth

Every business concept must have one authoritative source.

Examples:

```text
Product
→ Catalog persistence

Seller
→ Sellers persistence

Offer
→ Offers persistence

Price
→ Pricing persistence

Availability
→ Availability persistence

FulfillmentNode
→ Fulfillment persistence

Search Document
→ Search projection

AI Enrichment Proposal
→ AI operational store
```

A projection is never automatically authoritative.

---

# 8. Canonical Store vs Projection

Canonical stores preserve business state.

Projections optimize access.

Conceptually:

```text
Canonical Domain State
        │
        ▼
Integration Event
        │
        ▼
Projection
        │
        ├── Redis
        ├── Elasticsearch
        └── Other read model
```

Projection data may be rebuilt.

Canonical state must not depend on a projection being available.

---

# 9. Catalog Persistence

Catalog is a strong candidate for MongoDB.

Reasons include:

```text
flexible product structures
category-specific attributes
variable specifications
nested media metadata
document-oriented product representation
rapid schema evolution
```

A possible initial data layout:

```text
catalog database

├── products
├── categories
├── brands
└── outbox
```

Final collection boundaries must follow Aggregate design.

---

# 10. Product Document

A Product persistence document may conceptually contain:

```text
ProductId
Name
Description
BrandId
CategoryId
Attributes
Specifications
Media
Status
Version
Audit metadata
```

If SKU remains inside the Product Aggregate, SKU state may be persisted within the Product document.

If SKU becomes an independent Aggregate, persistence must reflect that decision.

The database model follows the Aggregate boundary, not the other way around.

---

# 11. MongoDB Modeling Rule

MongoDB documents must not force oversized Aggregates.

Avoid:

```text
Product
└── millions of unrelated commercial records
```

MongoDB embedding is appropriate only when lifecycle and transactional consistency justify it.

---

# 12. Catalog Indexes

Potential MongoDB indexes may include:

```text
ProductId
Status
CategoryId
BrandId
ExternalReference
SKU identifiers
GTIN/EAN
```

Exact indexes must follow real query patterns.

Do not create speculative indexes without access-pattern evidence.

---

# 13. Catalog Schema Evolution

Document schema evolution must be version-aware.

Potential strategies include:

```text
SchemaVersion field
backward-compatible readers
background migrations
write-forward migration
lazy migration
```

Breaking changes must not assume every document can be rewritten instantly.

---

# 14. Sellers Persistence

Sellers is a strong candidate for relational persistence.

Reasons include:

```text
structured identity
lifecycle state
external references
legal/commercial data
uniqueness constraints
transactional updates
```

Potential store:

```text
SQL Server
PostgreSQL
Azure SQL
```

---

# 15. Sellers Relational Model

Conceptually:

```text
Seller

SellerId
SellerType
SellerStatus
DisplayName
CreatedAtUtc
UpdatedAtUtc
Version
```

External references may live in a related table:

```text
SellerExternalReference

SellerId
System
Type
Value
```

Exact schema belongs to Infrastructure.

---

# 16. Offers Persistence

Offers may become a very large dataset.

Potential persistence candidates:

```text
SQL Server
PostgreSQL
MongoDB
```

Selection depends on:

```text
write volume
query patterns
uniqueness rules
seller synchronization
bulk updates
partitioning
history requirements
```

A relational store is a reasonable initial candidate if strict uniqueness and lifecycle constraints dominate.

---

# 17. Offer Access Patterns

Likely queries include:

```text
Offer by OfferId
Offers by SkuId
Offers by SellerId
Active Offers by SkuId
Offer by external reference
```

These access patterns should drive indexing strategy.

---

# 18. Pricing Persistence

Pricing is a strong candidate for relational storage.

Reasons include:

```text
financial precision
validity ranges
uniqueness constraints
concurrency
auditing
history
structured queries
```

Potential store:

```text
SQL Server
PostgreSQL
Azure SQL
```

---

# 19. Pricing Data Model

Conceptual relational entities may include:

```text
Price

PriceId
OfferId
Scope
RegionId
Amount
Currency
ValidFromUtc
ValidUntilUtc
Status
Version
```

Payment conditions may be normalized or persisted as owned structured records depending on Domain design.

The persistence model must not drive the Domain model.

---

# 20. Pricing History

Current pricing and price history have different access patterns.

Potential strategy:

```text
Current Price state
+
Price History
```

History may be implemented through:

```text
versioned records
audit table
temporal tables
event log
append-only history
```

The exact mechanism should be documented by ADR.

---

# 21. Availability Persistence

Availability is expected to be:

```text
high write frequency
high read frequency
high cardinality
event-driven
latency-sensitive
```

MongoDB is a strong initial candidate for canonical Availability state.

Redis is a strong candidate for low-latency read projection/cache.

---

# 22. Availability Canonical Store

Potential canonical key:

```text
SkuId or OfferId
+
FulfillmentNodeId
```

Conceptual document:

```text
Availability

AvailabilityId
CommercialReference
FulfillmentNodeId
OnHandQuantity
ReservedQuantity
SellableQuantity
Status
SourceVersion
UpdatedAtUtc
```

The exact key must follow the Availability Domain decision.

---

# 23. Availability Indexes

Likely indexes include:

```text
AvailabilityId
SkuId
OfferId
FulfillmentNodeId
Status
SourceVersion
```

Compound indexes should reflect actual access patterns.

---

# 24. Availability Current State vs History

High-frequency current state should remain compact.

Historical movements may be stored separately.

Conceptually:

```text
availability_current

availability_history
```

or equivalent collections/tables.

Do not force full movement history into the current-state Aggregate.

---

# 25. Fulfillment Persistence

Fulfillment is a strong candidate for relational persistence.

Reasons include:

```text
structured master data
node lifecycle
capabilities
external references
location metadata
uniqueness
```

Potential store:

```text
SQL Server
PostgreSQL
Azure SQL
```

---

# 26. Geospatial Fulfillment Data

Future service-area and location queries may require geospatial storage.

Potential options include:

```text
PostGIS
SQL spatial types
Elasticsearch geo capabilities
specialized geospatial service
```

The Domain must remain independent from the chosen engine.

---

# 27. Freight Persistence

Not all Freight Quotes must be persisted.

Freight data may include:

```text
Freight policies
Carrier configuration references
Persisted quote snapshots
Audit data
```

Many quotation operations may remain ephemeral.

Persistence requirements must be driven by:

```text
checkout consistency
audit
commercial guarantee
support
carrier reconciliation
```

---

# 28. AI Persistence

AI requires its own operational data.

Potential concepts include:

```text
AI Enrichment Request
AI Enrichment Proposal
Prompt execution metadata
Provider
Model
Token usage
Cost estimate
Evaluation result
Approval state
Embedding metadata
```

AI operational state must not become Catalog source of truth.

---

# 29. AI Enrichment Store

A potential structure:

```text
AIEnrichment

EnrichmentId
ProductId
Status
Provider
Model
InputReference
ProposedData
Confidence
CreatedAtUtc
CompletedAtUtc
ApprovedAtUtc
```

MongoDB is a strong candidate because enrichment payloads may be semi-structured.

---

# 30. Search Persistence

Elasticsearch is the initial Search engine.

It stores denormalized read models optimized for product discovery.

Search is not a system of record.

---

# 31. Product Search Document

A Search document may combine:

```text
Product
SKU
Brand
Category
Attributes
Seller
Offer
Price
Availability
Fulfillment signals
Keywords
Embeddings
```

This duplication is intentional.

It exists to optimize reads.

---

# 32. Search Projection Ownership

Search owns the projection document.

It does not own the underlying business concepts.

If Search data is corrupted, it should be rebuildable from authoritative sources and events.

---

# 33. Search Rebuild Strategy

The architecture should support rebuilding Elasticsearch indexes.

Potential approaches:

```text
event replay
authoritative snapshot export
projection rebuild job
bulk reindex pipeline
```

A rebuild must not require manually reconstructing business truth from Elasticsearch.

---

# 34. Search Index Versioning

Search indexes should support versioned rollout.

Example:

```text
products-v1
products-v2
```

with alias switching:

```text
products-current
```

This enables mapping changes with reduced downtime.

---

# 35. Elasticsearch Mapping

Mappings should be explicit for important fields.

Potential field types include:

```text
keyword
text
numeric
date
boolean
nested
geo_point
dense_vector
```

Dynamic mapping should not be relied on blindly for critical commerce fields.

---

# 36. Elasticsearch and Prices

Search may contain customer-facing price projections.

Example:

```text
regularPrice
salePrice
pixPrice
lowestPrice
```

These are denormalized fields.

Pricing remains authoritative.

---

# 37. Elasticsearch and Availability

Search may contain simplified fields such as:

```text
isAvailable
availableRegions
pickupAvailable
```

Avoid reindexing for every insignificant quantity change when customer-visible search state remains unchanged.

---

# 38. Vector Search

Elasticsearch may initially provide vector capabilities for AI-assisted product discovery.

Potential embedded content:

```text
product title
description
attributes
specifications
category
FAQ
manual content
```

Vector infrastructure must remain replaceable.

---

# 39. Embedding Storage

Embedding vectors may be stored in Search indexes or another vector store.

The architecture should keep:

```text
EmbeddingProvider
```

separate from:

```text
VectorStore
```

Changing one must not force changing the other.

---

# 40. Redis Role

Redis is primarily used for low-latency, ephemeral or derived state.

Potential use cases:

```text
Pricing cache
Availability cache
Freight cache
Search cache
Distributed locks
Rate limiting
AI semantic cache
Session-like technical state
```

Redis is not automatically authoritative.

---

# 41. Cache-Aside

A common pattern may be:

```text
Application
    │
    ▼
Redis
    │
    ├── Hit
    │
    └── Miss
          │
          ▼
    Authoritative Store
          │
          ▼
        Redis
```

This is appropriate where stale-read tolerance and invalidation are understood.

---

# 42. Event-Updated Cache

Some cache entries may be updated asynchronously.

Example:

```text
PriceChanged
     │
     ▼
Kafka
     │
     ▼
Price Cache Consumer
     │
     ▼
Redis
```

This can reduce synchronous cache invalidation coupling.

---

# 43. Cache TTL

TTL must be domain-specific.

Example tendencies:

```text
Catalog metadata
→ longer TTL

Price
→ shorter TTL

Availability
→ very short TTL or event-maintained

Freight Quote
→ request-context dependent TTL
```

Exact values belong to operational configuration.

---

# 44. Cache Key Design

Cache keys are Infrastructure details.

They must be:

```text
stable
namespaced
versionable
collision-resistant
```

Domain code must not manipulate raw Redis key formats.

---

# 45. Cache Invalidation

Cache invalidation strategy must be explicit for each capability.

Potential strategies:

```text
TTL
event-driven invalidation
event-driven refresh
versioned key
manual invalidation
```

Avoid pretending one universal cache policy fits all domains.

---

# 46. Cache Failure Semantics

The system must define behavior when Redis is unavailable.

Possible responses include:

```text
fallback to authoritative store
degraded performance
temporary failure
partial functionality
```

Cache failure should not automatically imply data loss.

---

# 47. Distributed Locks

Redis may provide distributed locking where truly necessary.

Use distributed locks only when:

```text
a concrete coordination problem exists
```

Do not add locks preemptively.

Lock abstractions must remain provider-independent.

---

# 48. Object Storage

Object storage may be used for:

```text
product images
manuals
import files
export files
AI source documents
large payload references
```

Potential providers include:

```text
Azure Blob Storage
Google Cloud Storage
local development storage
```

Binary content should not be embedded into Kafka messages or relational rows by default.

---

# 49. Object References

Domain/Application should use provider-neutral references.

Conceptually:

```text
ObjectReference

ObjectId
ContentType
Location
Metadata
```

Provider-specific URLs or SDK types should remain in Infrastructure where practical.

---

# 50. Data Consistency

Strong consistency should generally remain within:

```text
Aggregate boundary
+
owning persistence transaction
```

Cross-context consistency is generally eventual.

---

# 51. Cross-Context Eventual Consistency

Example:

```text
Pricing updated
      │
      ▼
PriceChanged
      │
      ▼
Search projection updated
      │
      ▼
Redis projection updated
```

For a short period, those projections may lag.

This is expected.

---

# 52. Read-Your-Writes

When a use case requires immediate read-after-write consistency, it should query the authoritative context.

Example:

```text
Change Price
    │
    ▼
Return canonical updated Price
```

Do not depend immediately on Redis or Elasticsearch propagation.

---

# 53. Transaction Boundaries

A transaction should normally be local to one Bounded Context and one Aggregate consistency boundary.

Avoid distributed database transactions across:

```text
Catalog
Pricing
Availability
Search
```

Use EDA and eventual consistency.

---

# 54. Transactional Outbox Storage

Each context that publishes reliable events may have its own Outbox storage.

Examples:

```text
catalog_outbox
pricing_outbox
availability_outbox
```

Outbox records belong to the producing context.

---

# 55. Inbox Storage

Consumers requiring duplicate protection may maintain Inbox state.

Example:

```text
consumer_inbox

EventId
Consumer
ProcessedAtUtc
```

Inbox data is technical consumer state.

---

# 56. Idempotency Data

Idempotency keys may be persisted for:

```text
external API writes
event consumers
bulk imports
AI requests
```

Retention must be designed according to replay and duplication windows.

---

# 57. Optimistic Concurrency

Optimistic concurrency should be preferred where practical.

Potential mechanisms:

```text
Version
ETag
RowVersion
Mongo version field
```

The Domain should only understand concurrency semantics that are business-relevant.

---

# 58. Data Version

A generic version property may support:

```text
optimistic concurrency
stale update detection
event generation
```

Implementation details must remain store-specific.

---

# 59. Migrations

Relational schema changes must use controlled migrations.

Potential tooling:

```text
EF Core migrations
Flyway
DbUp
Liquibase
custom migration tooling
```

The chosen mechanism must be documented by ADR.

Production databases must not rely on destructive automatic initialization.

---

# 60. MongoDB Migrations

MongoDB schema evolution may use:

```text
background migration scripts
schema versioning
lazy migration
dual-read compatibility
```

Migration strategy must account for large collections.

---

# 61. Elasticsearch Migrations

Elasticsearch mapping changes may require:

```text
new index version
bulk reindex
alias switch
old index cleanup
```

Direct destructive mapping changes should be avoided.

---

# 62. Redis Versioning

Cache structure changes may use:

```text
versioned key prefixes
parallel key generations
TTL expiration
```

Avoid requiring a global Redis flush for ordinary application releases.

---

# 63. Seed Data

Seed data must be controlled.

Appropriate seed candidates may include:

```text
reference categories
development-only test data
known configuration
```

Production business records must not be silently recreated by application startup.

---

# 64. Development Data

Local development may use:

```text
Docker Compose
seed scripts
Testcontainers
```

Development data must remain clearly separated from production migration logic.

---

# 65. Data Security

Sensitive data must be protected.

Controls may include:

```text
encryption at rest
encryption in transit
least-privilege credentials
private networking
secret management
RBAC
auditing
```

Database credentials must never be committed to source control.

---

# 66. Data Classification

The project should eventually classify data such as:

```text
Public
Internal
Confidential
Restricted
```

This is especially important for:

```text
Seller legal data
customer-related future data
AI prompt content
integration payloads
```

---

# 67. Sensitive Data in Search

Do not index sensitive data in Elasticsearch unless explicitly required.

Search indexes are optimized for discovery, not confidential record storage.

---

# 68. Sensitive Data in Redis

Do not cache sensitive data indiscriminately.

Cached sensitive data requires:

```text
appropriate TTL
access control
encryption strategy if applicable
minimal payload
```

---

# 69. Sensitive Data in AI

AI prompts and context must not automatically include:

```text
credentials
access tokens
private keys
unnecessary personal information
restricted commercial data
```

AI Data Governance will be detailed in AI Architecture.

---

# 70. Backup

Authoritative stores require backup strategies.

Potential concerns:

```text
backup frequency
point-in-time recovery
retention
cross-region recovery
restore testing
```

Backup policy belongs to Platform/Data operations.

---

# 71. Restore Testing

A backup is not considered reliable until restore procedures are tested.

Recovery drills should eventually validate:

```text
relational restore
Mongo restore
projection rebuild
Redis regeneration
Elasticsearch reindex
```

---

# 72. Disaster Recovery

Disaster recovery strategy must distinguish:

```text
authoritative data
rebuildable projection
cache
event stream
object storage
```

Redis and Elasticsearch may often be rebuilt.

Canonical stores require stronger recovery guarantees.

---

# 73. Recovery Point Objective

RPO requirements may differ by context.

Example tendencies:

```text
Pricing
→ low tolerated data loss

Availability
→ very low current-state loss tolerance

Search
→ rebuildable

Redis
→ rebuildable

AI enrichment proposal
→ depends on workflow criticality
```

Exact targets must be defined before production.

---

# 74. Recovery Time Objective

RTO requirements also differ.

Customer-facing Pricing and Availability likely require faster recovery than analytics or enrichment history.

---

# 75. Data Retention

Retention must be explicit.

Potential examples:

```text
Price history
Availability history
Outbox
Inbox
AI execution metadata
Audit logs
Search old indexes
```

Indefinite retention must not be assumed.

---

# 76. Outbox Retention

Published Outbox records should eventually be archived or deleted according to operational policy.

Retention must preserve required auditability without allowing unbounded growth.

---

# 77. Inbox Retention

Inbox records must remain long enough to cover realistic duplicate/replay windows.

Retention strategy should be context-specific.

---

# 78. Data Archiving

Historical data may move to cheaper storage when no longer required for transactional access.

Potential examples:

```text
old Price history
old Availability movements
old AI execution records
integration archives
```

---

# 79. Data Access Layer

Infrastructure implements data access.

Domain and Application must not reference:

```text
DbContext
MongoCollection
Redis IDatabase
Elasticsearch client
SQL Connection
```

Ports and repositories define required capabilities.

---

# 80. Repository Pattern

Repositories are appropriate for Aggregate persistence.

Example:

```text
IProductRepository
ISellerRepository
IOfferRepository
IPriceRepository
IAvailabilityRepository
IFulfillmentNodeRepository
```

Not every query needs a Repository.

---

# 81. Query Services

Read-heavy use cases may use dedicated query ports.

Examples:

```text
IProductReadModel
IPriceQuery
IAvailabilityQuery
IProductSearch
```

This avoids forcing read models through Aggregate repositories.

---

# 82. ORM Strategy

Relational Infrastructure may use:

```text
Entity Framework Core
Dapper
ADO.NET
```

Different contexts may use different data-access technologies if justified.

The Domain must not depend on the ORM.

---

# 83. EF Core

EF Core is appropriate where:

```text
aggregate persistence
transaction handling
migrations
structured relational modeling
```

provide value.

Avoid leaking EF entities into Domain when it distorts the model.

---

# 84. Dapper

Dapper may be useful for:

```text
optimized read models
high-performance queries
simple projections
```

It should not become a reason to bypass Domain consistency on writes.

---

# 85. MongoDB Driver

MongoDB.Driver belongs only to Infrastructure.

Domain models must not require:

```text
Bson attributes
Mongo-specific IDs
Mongo-specific interfaces
```

unless a deliberate documented exception exists.

---

# 86. Elasticsearch Client

Elasticsearch client libraries belong only to Search Infrastructure.

Application search contracts must remain provider-neutral.

---

# 87. Redis Client

Redis client libraries belong only to Infrastructure.

Application should use:

```text
cache abstractions
```

rather than Redis-native types.

---

# 88. Connection Management

Database connections must be configured centrally per Infrastructure module.

Configuration should use strongly typed options.

Connection strings belong to secure configuration.

---

# 89. Secrets

Production secrets may use:

```text
Azure Key Vault
Managed Identity
Workload Identity
Kubernetes Secrets
```

Hardcoded credentials are forbidden.

---

# 90. Health Checks

Authoritative data stores and critical projections should expose health/readiness information.

Examples:

```text
SQL connectivity
Mongo connectivity
Redis connectivity
Elasticsearch connectivity
Kafka connectivity
```

Readiness strategy must distinguish critical dependencies from optional/degraded ones.

---

# 91. Observability

Data operations should emit telemetry.

Potential metrics include:

```text
query latency
write latency
connection failures
timeouts
cache hit rate
cache miss rate
Mongo operation duration
SQL duration
Elasticsearch indexing latency
projection lag
reindex duration
```

---

# 92. Slow Queries

Slow-query monitoring should be enabled where supported.

Optimization must follow evidence.

Do not prematurely denormalize authoritative models without measurements.

---

# 93. Capacity Planning

Data architecture must consider:

```text
Products
SKUs
Sellers
Offers
Prices
Availability records
Fulfillment Nodes
Search documents
Events
AI executions
```

Expected cardinality should be documented as real traffic/load assumptions become available.

---

# 94. Horizontal Scaling

Contexts with potentially high cardinality, such as:

```text
Offers
Availability
Search
```

must be designed with partitioning/sharding possibilities in mind.

This must not leak infrastructure mechanics into Domain behavior.

---

# 95. Database Partitioning

Potential partition keys should follow access patterns and hotspot analysis.

Do not choose partition keys merely because an identifier exists.

---

# 96. Data Locality

Where practical, data accessed together frequently should be colocated within the same Aggregate persistence boundary.

Cross-context locality must not override Domain ownership.

---

# 97. Denormalization

Denormalization is encouraged in read projections where it improves query performance.

Examples:

```text
Search document
Customer product read model
Availability read projection
```

Denormalization must not silently create multiple writable sources of truth.

---

# 98. Materialized Read Models

The architecture may create specialized read models for:

```text
Product Details
Offer Summary
Current Price
Regional Availability
Customer Commerce View
```

Read models may combine multiple contexts.

They remain derived data.

---

# 99. Customer Commerce Projection

A future customer-facing projection may combine:

```text
Product
SKU
Seller
Offer
Price
Availability
Fulfillment signals
Freight summary
```

This does not mean those concepts belong to one Aggregate or one database.

---

# 100. Projection Rebuild

Every derived projection should define how it can be rebuilt.

Questions include:

```text
What is the authoritative source?

Can Kafka replay rebuild it?

Is a bulk snapshot required?

How long does rebuild take?

Can it be rebuilt online?
```

---

# 101. Data Contracts

Persistence models are private.

Integration contracts are explicit.

Never expose raw database documents or ORM entities directly through APIs or events.

---

# 102. Data Serialization

Serialization formats should be selected per boundary.

Examples:

```text
JSON for APIs/events initially
BSON for MongoDB internally
provider-specific binary formats inside Infrastructure
```

Serialization choices must not alter Domain semantics.

---

# 103. IDs

Canonical identifiers should remain independent from database-native identifiers.

Avoid allowing:

```text
Mongo ObjectId
SQL identity
Elasticsearch document ID
```

to define business identity automatically.

Strongly typed IDs are preferred where they add semantic safety.

---

# 104. GUID/UUID Strategy

UUID/GUID is a strong candidate for many canonical IDs because it:

```text
supports distributed creation
avoids central identity generation
is database-independent
```

The exact generation strategy should be standardized.

---

# 105. Time

Persisted system timestamps must use UTC unless a documented business rule requires otherwise.

Prefer:

```text
DateTimeOffset
```

for system boundaries.

Business timezone conversion occurs explicitly.

---

# 106. Money

Monetary persistence must preserve decimal precision.

Never store canonical financial values as binary floating point.

Currency must be stored when semantics require it.

---

# 107. Nullability

Database nullability must reflect Domain semantics.

Do not allow nullable columns/fields merely because migration tooling makes it convenient.

---

# 108. Referential Integrity

Within one relational context, database foreign keys may reinforce Domain constraints.

Across Bounded Contexts, database foreign keys must not create hidden coupling.

Example:

```text
Pricing.OfferId
```

should not require a physical SQL FK into Offers database.

Cross-context references are logical.

---

# 109. Unique Constraints

Persistence constraints should reinforce business invariants where practical.

Examples:

```text
unique external reference
unique GTIN where appropriate
unique Seller external mapping
```

The Domain must still protect important invariants.

Database constraints are a second line of defense.

---

# 110. Check Constraints

Relational check constraints may reinforce simple invariants such as:

```text
Amount >= 0
```

when appropriate.

They do not replace Domain rules.

---

# 111. Data Import

Large imports should use explicit pipelines.

Potential flow:

```text
File/Object Storage
      │
      ▼
Integration Worker
      │
      ▼
Canonical Mapping
      │
      ▼
Application Use Cases
      │
      ▼
Domain Persistence
```

Raw external schemas should not be bulk-written directly into canonical tables/collections.

---

# 112. Data Export

Exports should use read models optimized for export.

Do not serialize Aggregate internals directly merely because they are available.

---

# 113. Data Reconciliation

External integrations require reconciliation capability.

Potential processes include:

```text
Catalog reconciliation
Offer reconciliation
Price reconciliation
Availability reconciliation
Fulfillment reconciliation
```

Streaming integration alone must not be assumed perfect.

---

# 114. Snapshot Reconciliation

A robust integration may combine:

```text
real-time events
+
periodic snapshots
```

to detect and correct drift.

---

# 115. Audit

Critical business changes should support auditability.

Particularly important:

```text
Pricing
Seller lifecycle
Offer lifecycle
Fulfillment lifecycle
Availability corrections
AI-approved changes
```

Audit records should remain separate from ordinary application logs.

---

# 116. Data Governance

As the platform matures, Data Governance should define:

```text
ownership
classification
retention
lineage
quality
access
recovery
```

This documentation is part of architecture, not merely operations.

---

# 117. Data Lineage

AI and Integration workflows especially benefit from lineage.

Example:

```text
External ERP Product
      │
      ▼
Canonical Product
      │
      ▼
AI Enrichment
      │
      ▼
Approved Catalog Update
      │
      ▼
Search Projection
```

The system should preserve enough metadata to explain where important data originated.

---

# 118. Data Quality

Catalog and integration pipelines may evaluate:

```text
missing values
invalid identifiers
duplicate data
conflicting attributes
stale data
inconsistent external mappings
```

Data quality findings must not automatically bypass Domain validation.

---

# 119. AI Data Quality

AI may suggest corrections.

It must not silently rewrite authoritative data.

Conceptual flow:

```text
Detected Problem
      │
      ▼
AI Suggestion
      │
      ▼
Validation
      │
      ▼
Approved Change
```

---

# 120. Initial Database Assignment

The initial recommended mapping is:

```text
Catalog
→ MongoDB

Sellers
→ Relational Database

Offers
→ Relational Database initially

Pricing
→ Relational Database

Availability
→ MongoDB canonical state
   + Redis projection/cache

Fulfillment
→ Relational Database

Freight
→ Minimal persistence initially
   + Redis where quote caching is useful

Search
→ Elasticsearch

AI
→ MongoDB or another document-oriented operational store

Object assets
→ Object Storage

Events
→ Kafka
```

This is the initial architecture direction.

Specific technology/vendor choices must be confirmed through ADRs.

---

# 121. Local Development Data Stack

Local development should eventually support Docker Compose for:

```text
MongoDB
Relational Database
Redis
Elasticsearch
Kafka
```

Cloud object storage and AI services may use:

```text
local substitutes where practical
or
development cloud resources
```

---

# 122. Testcontainers

Integration tests should prefer Testcontainers for data infrastructure where practical.

Examples:

```text
MongoDB
SQL Server/PostgreSQL
Redis
Elasticsearch
Kafka
```

This reduces dependence on developer-machine configuration.

---

# 123. Architecture Tests

Architecture tests should verify that:

```text
Domain does not reference data providers

Application does not reference MongoDB.Driver

Application does not reference EF Core implementations

Search.Application does not reference Elasticsearch client libraries

Domain does not reference Redis client libraries
```

---

# 124. Migration Ownership

Each Bounded Context owns its migrations.

Example:

```text
Pricing.Infrastructure
    owns Pricing migrations

Sellers.Infrastructure
    owns Sellers migrations
```

Avoid one global migration project controlling every context.

---

# 125. Database Deployment

Database migrations should execute through controlled deployment workflows.

Application startup should not perform destructive schema migration automatically in production.

---

# 126. Data ADRs

Important data decisions should create ADRs.

Initial candidates include:

```text
ADR - Catalog uses MongoDB

ADR - Pricing uses relational persistence

ADR - Availability uses MongoDB plus Redis projection

ADR - Search uses Elasticsearch

ADR - Redis is not a source of truth

ADR - Canonical IDs are database-independent

ADR - Database ownership follows Bounded Contexts
```

---

# 127. Architecture Questions Before Implementation

Before adding actual database adapters, explicitly decide:

```text
Which relational database will be used initially?

SQL Server or PostgreSQL?

Which MongoDB topology is required locally and in cloud?

Will Offers initially use relational persistence?

What is the final Availability canonical key?

What Availability state goes to Redis?

What Search document structure is required?

Will Elasticsearch also store vectors initially?

Where will AI operational data be stored?

Which object storage provider is used first?

How are migrations executed in CI/CD?

How are backups and restores tested?

What data retention is required?

What audit history is required?

What scale assumptions should drive indexes?
```

---

# 128. Initial Data Implementation Sequence

Recommended implementation order:

```text
1. Catalog MongoDB adapter

2. Catalog Outbox persistence

3. Kafka publication

4. Elasticsearch Search projection

5. AI operational persistence

6. Sellers relational persistence

7. Offers relational persistence

8. Pricing relational persistence

9. Availability canonical persistence

10. Redis Availability projection

11. Fulfillment relational persistence

12. Freight cache/persistence as required
```

This sequence follows the first Catalog + AI + Search vertical slice.

---

# 129. Core Rule

The core Data Architecture rule is:

> Business ownership determines where data belongs. Access patterns determine how data is stored and projected.

Not:

> We have MongoDB, so everything becomes a document.

Not:

> We have SQL Server, so every context shares one schema.

Not:

> Elasticsearch is fast, so it becomes the database.

Not:

> Redis is fast, so it becomes the source of truth.

---

# 130. Final Principle

Yunu.Commerce Data Architecture must remain:

```text
domain-owned
polyglot
projection-friendly
cache-aware
search-aware
AI-ready
auditable
recoverable
scalable
migration-aware
provider-decoupled
```

Databases may change.

Search engines may change.

Cache technology may change.

Cloud infrastructure may change.

The ownership and semantics of business data must remain protected.
