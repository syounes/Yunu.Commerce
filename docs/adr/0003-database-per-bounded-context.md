# ADR-0003: Database per Bounded Context

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Persistence ownership and database boundaries

## 1. Context

Yunu.Commerce is divided into independent Bounded Contexts:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

Each context has different consistency requirements, access patterns, data volumes and scalability characteristics.

The platform also uses supporting data technologies such as Redis and Elasticsearch.

A single shared database model would create hidden coupling between contexts and allow implementation details to bypass the Domain boundaries established by DDD.

## 2. Decision

Yunu.Commerce adopts:

> Database ownership per Bounded Context.

This means each Bounded Context exclusively owns its canonical persisted data.

The rule describes **logical ownership**, not necessarily one physical database server for every context on day one.

Conceptually:

```text
Catalog
    │
    ▼
Catalog Data

Sellers
    │
    ▼
Sellers Data

Offers
    │
    ▼
Offers Data

Pricing
    │
    ▼
Pricing Data

Availability
    │
    ▼
Availability Data

Fulfillment
    │
    ▼
Fulfillment Data

Freight
    │
    ▼
Freight Data
```

## 3. Core Ownership Rule

Only the owning context may mutate its canonical data.

For example:

```text
Catalog owns Product and SKU data.

Pricing owns Price data.

Availability owns sellable availability data.
```

Pricing must not update Catalog collections.

Catalog must not update Availability records.

Availability must not update Fulfillment tables.

## 4. No Cross-Context Database Access

Direct cross-context database access is forbidden.

Forbidden:

```text
Pricing
   │
   ▼
Catalog.Products table
```

Forbidden:

```text
Offers
   │
   ▼
Sellers database
```

Forbidden:

```text
Freight
   │
   ▼
Availability MongoDB collection
```

Communication must use explicit contracts.

## 5. Allowed Cross-Context Communication

Allowed mechanisms include:

```text
Integration Events
Application/API contracts
Read projections
Explicit context-owned queries
Process Managers when required
```

The database is never the integration contract.

## 6. Logical vs Physical Isolation

Initially, multiple contexts may share a physical database server or cluster for operational simplicity.

Example:

```text
SQL Server Instance
│
├── YunuSellers
├── YunuOffers
├── YunuPricing
└── YunuFulfillment
```

or separate schemas where appropriate:

```text
Database
│
├── sellers.*
├── offers.*
├── pricing.*
└── fulfillment.*
```

However, ownership must remain enforceable.

Sharing infrastructure does not mean sharing data models.

## 7. Future Physical Separation

Because ownership is explicit, a context can later move from:

```text
Shared SQL infrastructure
```

to:

```text
Dedicated SQL database
```

without redesigning the Domain.

The same principle applies to MongoDB clusters and other persistence infrastructure.

## 8. Polyglot Persistence

Database-per-context does not mean every context must use the same database technology.

Yunu.Commerce explicitly permits polyglot persistence.

Initial direction:

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
→ MongoDB

Fulfillment
→ Relational Database

Freight
→ Persistence only where required
```

Supporting stores:

```text
Redis
→ cache / low-latency projections

Elasticsearch
→ search projections / vector search

Object Storage
→ files, media and large source artifacts
```

## 9. Catalog

Catalog is initially mapped to MongoDB.

Reasons include:

```text
flexible product attributes
category-specific structures
nested specifications
schema evolution
document-oriented Product representation
```

The exact Aggregate structure determines document boundaries.

## 10. Sellers

Sellers is initially mapped to a relational database.

Reasons include:

```text
structured identity
uniqueness constraints
seller lifecycle
external references
transactional consistency
```

## 11. Offers

Offers initially use relational persistence.

Reasons include:

```text
Seller/SKU uniqueness rules
structured lifecycle
commercial references
high-value indexed queries
transactional consistency
```

This choice may be revisited if production access patterns indicate a better fit.

## 12. Pricing

Pricing uses relational persistence.

Reasons include:

```text
financial precision
validity ranges
auditing
history
concurrency
structured constraints
```

Price values must use appropriate decimal precision.

## 13. Availability

Availability initially uses MongoDB as canonical storage.

Reasons include:

```text
high cardinality
frequent updates
node/regional state
event-oriented ingestion
horizontal scaling potential
```

Redis may provide low-latency derived availability views.

Redis is not the canonical Availability store.

## 14. Fulfillment

Fulfillment initially uses relational persistence.

Reasons include:

```text
structured master data
branch/node lifecycle
capabilities
location references
external identifiers
```

## 15. Freight

Freight persistence will remain minimal until concrete requirements justify additional canonical storage.

Potential persisted information includes:

```text
freight policies
provider configuration references
quote snapshots when required
audit/reconciliation state
```

Redis may cache appropriate quotation results.

## 16. Search

Elasticsearch is not a canonical business database.

It owns derived Search projections.

Conceptually:

```text
Canonical Contexts
       │
       ▼
Integration Events
       │
       ▼
Search Projection
       │
       ▼
Elasticsearch
```

Elasticsearch data must be rebuildable.

## 17. Redis

Redis is not a canonical source of truth by default.

It is used for:

```text
cache
low-latency projections
temporary technical state
rate limiting
distributed coordination where justified
```

Redis loss must not imply loss of canonical commerce data.

## 18. AI Data

AI operational data is separate from canonical commerce state.

Possible AI data includes:

```text
AI execution metadata
enrichment proposals
prompt version
model/provider metadata
token usage
evaluation results
```

AI-generated proposals do not become Catalog data until accepted through Catalog Application and Domain rules.

## 19. Referential Integrity Across Contexts

Database foreign keys must not cross Bounded Context ownership boundaries.

Example:

```text
Pricing.Price.OfferId
```

may reference an Offer logically.

It must not require a physical foreign key into the Offers database.

Cross-context identity is contractual, not relational-database ownership.

## 20. Referential Integrity Within a Context

Inside a context, database constraints may reinforce Domain rules.

Examples:

```text
unique constraints
foreign keys
check constraints
row versions
```

when appropriate.

Database constraints complement Domain invariants.

They do not replace them.

## 21. Cross-Context Joins

Cross-context database joins are forbidden for application behavior.

Forbidden:

```sql
SELECT *
FROM pricing.Price p
JOIN catalog.Product c ON ...
JOIN sellers.Seller s ON ...
```

For combined customer views, use:

```text
projection
API composition
dedicated read model
```

## 22. Read Projections

A projection may combine data from multiple contexts.

Example:

```text
Customer Product View

Product
+
SKU
+
Seller
+
Offer
+
Price
+
Availability
```

This projection may be stored in Elasticsearch or another read store.

It remains derived data.

## 23. Eventual Consistency

Because contexts own separate data, cross-context state is generally eventually consistent.

Example:

```text
Pricing
  │
  ▼
Price committed
  │
  ▼
PriceChanged
  │
  ▼
Kafka
  │
  ▼
Search projection updated
```

A small propagation delay is expected.

## 24. Local Transactions

Transactions must normally remain inside one Bounded Context.

Example:

```text
Pricing transaction

Price Aggregate
+
Pricing Outbox
```

This can be committed atomically inside Pricing persistence.

## 25. Distributed Transactions

Distributed transactions across context databases are not part of the default architecture.

Avoid:

```text
2PC
distributed ACID transaction
cross-database transaction coupling
```

Use local transactions plus reliable messaging.

## 26. Transactional Outbox

Each event-producing context may own an Outbox in its persistence boundary.

Example:

```text
Catalog Database
│
├── Products
└── Outbox

Pricing Database
│
├── Prices
└── Outbox
```

This allows Aggregate state and outgoing event intent to commit together.

See ADR-0005.

## 27. Inbox

Consumers requiring durable deduplication may own Inbox state.

Inbox belongs to the consuming capability.

It must not be centralized into a global business database.

## 28. Migrations

Each context owns its persistence migrations.

Examples:

```text
Sellers.Infrastructure
→ Sellers migrations

Pricing.Infrastructure
→ Pricing migrations

Fulfillment.Infrastructure
→ Fulfillment migrations
```

There should not be one giant migration project that understands every context's schema.

## 29. MongoDB Schema Evolution

MongoDB contexts own their document evolution.

Strategies may include:

```text
SchemaVersion
backward-compatible readers
lazy migration
background migration
```

No other context may depend on the physical document structure.

## 30. Database Credentials

Production access should eventually support separate credentials/identities per context.

Example:

```text
Catalog workload
→ Catalog database permissions

Pricing workload
→ Pricing database permissions
```

Pricing should not receive write permissions to Catalog persistence.

## 31. Least Privilege

Database permissions should reinforce architecture.

Ideal future state:

```text
Catalog
→ read/write Catalog only

Pricing
→ read/write Pricing only

Availability
→ read/write Availability only
```

Projection workers receive only the access they require.

## 32. Repository Ownership

Repository implementations belong to the owning context.

Example:

```text
Catalog.Infrastructure
└── MongoProductRepository

Pricing.Infrastructure
└── SqlPriceRepository
```

There is no global repository that exposes all commerce databases.

## 33. Domain Independence

Domain projects must not know which database they use.

Forbidden:

```text
Product : MongoDocument
```

Forbidden:

```text
Price contains EF Core attributes required for persistence behavior
```

Provider-specific mapping belongs to Infrastructure.

## 34. Canonical IDs

Business identifiers must remain database-independent.

Do not allow these to define business identity automatically:

```text
Mongo ObjectId
SQL IDENTITY
Elasticsearch document ID
Redis key
```

Canonical IDs should be explicit Domain concepts.

## 35. Database Technology Replacement

This architecture intentionally supports future replacement.

Example:

```text
Offers
SQL Server
   │
   ▼
PostgreSQL
```

or:

```text
Availability
MongoDB
   │
   ▼
another persistence engine
```

Such changes should primarily affect Infrastructure.

## 36. Database per Context Does Not Mean Repository per Table

The goal is Domain ownership, not CRUD abstraction.

Repositories should align with Aggregate boundaries.

Avoid:

```text
ProductTableRepository
ProductAttributeTableRepository
ProductImageTableRepository
```

when those records belong to one Product Aggregate.

## 37. Reporting and Analytics

Analytics must not become an excuse for cross-context transactional queries.

Future reporting may consume:

```text
events
CDC where explicitly approved
data lake
warehouse
analytical projections
```

Operational context databases remain protected.

## 38. Change Data Capture

CDC may be evaluated for legacy integration or analytics.

It must not become the default integration mechanism between Yunu.Commerce Bounded Contexts.

Business Integration Events are preferred because they carry semantic intent.

## 39. Backup and Recovery

Canonical context databases require backup and recovery strategies.

Derived stores such as:

```text
Redis
Elasticsearch
```

may often be rebuilt.

Recovery policies must reflect whether data is canonical or derived.

## 40. Observability

Database telemetry should be attributable to its owning context.

Examples:

```text
Catalog Mongo latency
Pricing SQL latency
Availability Mongo write rate
Redis cache hit rate
Search indexing latency
```

This improves independent capacity planning.

## 41. Scaling

Database-per-context allows contexts to scale according to their own workload.

Example:

```text
Catalog
→ document-heavy workload

Pricing
→ transactional relational workload

Availability
→ high-frequency state updates

Search
→ read/index-heavy workload
```

A single shared database architecture would force unrelated workloads to compete.

## 42. Deployment

Database ownership does not require immediate microservices.

A Modular Monolith can still maintain:

```text
separate schemas
separate databases
separate Mongo collections/databases
separate repositories
separate migrations
```

while running in one process.

## 43. Consequences

### Positive

```text
strong context ownership
reduced hidden coupling
polyglot persistence
independent migrations
independent scaling
future service extraction
clear security boundaries
better fault isolation potential
```

### Negative

```text
cross-context joins are unavailable
eventual consistency is required
more infrastructure configuration
more migrations
duplicate projection data
more explicit integration contracts
```

These tradeoffs are accepted.

## 44. Alternatives Considered

### Single Shared Relational Database

Rejected as the architectural ownership model because it encourages cross-context joins and schema coupling.

### Single MongoDB Model for Everything

Rejected because several contexts have relational/transactional characteristics and because one technology should not define all Domain models.

### One Physical Database Server per Context Immediately

Not required initially.

Logical ownership is mandatory.

Physical isolation may increase as operational needs grow.

### Elasticsearch as Primary Database

Rejected.

Elasticsearch is a derived Search store.

### Redis as Primary Availability Database

Rejected initially.

Redis is a projection/cache layer while canonical Availability is persisted independently.

## 45. Architecture Enforcement

The decision should be enforced through:

```text
project boundaries
database permissions
separate migrations
repository ownership
architecture tests
code review
Copilot instructions
CI checks
```

## 46. Copilot Rules

GitHub Copilot must:

```text
Never query another Bounded Context's database.

Never create cross-context SQL joins.

Never create foreign keys across context databases.

Keep persistence implementations inside Infrastructure.

Keep provider-specific database types outside Domain.

Create migrations per owning context.

Use repositories/query ports belonging to the context.

Use Integration Events or explicit APIs for cross-context communication.

Treat Redis and Elasticsearch as derived stores unless explicitly documented otherwise.

Do not introduce a shared DbContext containing all Yunu.Commerce entities.

Do not create one generic repository spanning contexts.
```

## 47. Initial Persistence Map

The accepted initial direction is:

```text
┌──────────────────┬──────────────────────────────────┐
│ Capability       │ Initial Persistence Direction    │
├──────────────────┼──────────────────────────────────┤
│ Catalog          │ MongoDB                          │
│ Sellers          │ Relational                       │
│ Offers           │ Relational                       │
│ Pricing          │ Relational                       │
│ Availability     │ MongoDB                          │
│ Fulfillment      │ Relational                       │
│ Freight          │ As required                      │
│ Search           │ Elasticsearch projection         │
│ Cache            │ Redis                            │
│ AI Operations    │ Document-oriented store initially│
│ Assets           │ Object Storage                   │
└──────────────────┴──────────────────────────────────┘
```

The specific relational engine is a separate implementation/cloud decision.

## 48. Relationship to Other ADRs

This ADR depends on:

```text
ADR-0001
Use DDD, Clean Architecture and Hexagonal Architecture

ADR-0002
Bounded Context Strategy
```

It informs:

```text
ADR-0005
Transactional Outbox

ADR-0006
Redis for Distributed Cache

ADR-0007
Elasticsearch for Search Projections

ADR-0009
Cloud Provider Strategy
```

## 49. Final Decision

Yunu.Commerce adopts logical database ownership per Bounded Context.

Contexts may initially share physical infrastructure where operationally useful, but they must never share ownership.

The defining rule is:

> A Bounded Context owns its canonical data, schema, persistence contracts and migrations. Other contexts communicate with it through explicit contracts, not through its database.
