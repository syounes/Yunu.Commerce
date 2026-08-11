# Yunu.Commerce - Integration Architecture

## 1. Purpose

This document defines the Integration Architecture for Yunu.Commerce.

The platform is composed of independent Bounded Contexts and must integrate with internal modules, enterprise systems, marketplaces, logistics providers, AI providers and other external platforms without compromising Domain boundaries.

The integration architecture combines:

```text
Synchronous APIs
+
Asynchronous Events
+
Adapters / Anti-Corruption Layers
+
Reliable Messaging
```

The central rule is:

> Bounded Contexts communicate through explicit contracts. They never integrate through each other's databases.

---

# 2. Integration Principles

Yunu.Commerce follows these principles:

```text
Explicit contracts

Loose coupling

Event-driven integration where appropriate

Synchronous communication only when immediate answers are required

Idempotent consumers

Transactional Outbox

Inbox / deduplication where required

Schema evolution

Correlation and traceability

Provider-specific adapters

Anti-Corruption Layers

No shared database integration

No external DTOs inside Domain

Failure isolation

Eventual consistency across Bounded Contexts
```

---

# 3. Integration Styles

The platform supports three primary integration styles:

```text
1. Synchronous API integration

2. Asynchronous event integration

3. Batch / bulk integration
```

Each style solves a different problem.

---

# 4. Synchronous APIs

Synchronous APIs are appropriate when a caller requires an immediate response.

Examples:

```text
Get Product
Get Offer
Get Current Price
Get Availability
Quote Freight
Search Products
Submit AI Enrichment Request
```

REST over HTTPS is the initial external API style.

---

# 5. Asynchronous Integration

Asynchronous messaging is preferred when:

```text
the caller does not require an immediate result
multiple consumers react independently
high throughput is required
producer and consumer should evolve independently
temporary consumer unavailability must be tolerated
eventual consistency is acceptable
```

Kafka is the initial event-streaming platform.

---

# 6. Batch Integration

Batch processing is appropriate for:

```text
initial catalog imports
large seller feeds
inventory snapshots
price files
reconciliation
historical reprocessing
bulk AI enrichment
```

Batch integration complements streaming.

It does not replace real-time integration where freshness matters.

---

# 7. Kafka

Kafka is the initial event backbone for Yunu.Commerce.

Conceptually:

```text
Bounded Context
      │
      ▼
Transactional Outbox
      │
      ▼
Outbox Publisher
      │
      ▼
Kafka
      │
      ├── Consumer A
      ├── Consumer B
      └── Consumer C
```

Domain projects must never reference Kafka libraries.

---

# 8. Domain Events vs Integration Events

These concepts are different.

Domain Event:

```text
Something meaningful happened inside an Aggregate.
```

Integration Event:

```text
A fact is intentionally published outside the Bounded Context.
```

Example:

```text
Price.Change(...)

        │
        ▼

PriceChangedDomainEvent

        │
        ▼

Application / Event Mapper

        │
        ▼

PriceChanged Integration Event

        │
        ▼

Kafka
```

Not every Domain Event must become an Integration Event.

---

# 9. Integration Event Characteristics

Integration Events should be:

```text
immutable
explicitly named
versioned
self-describing
provider-neutral
business-oriented
safe for asynchronous processing
```

They represent facts that already happened.

---

# 10. Event Naming

Prefer past-tense business facts:

```text
ProductCreated
ProductUpdated
SkuCreated
SellerActivated
OfferActivated
PriceChanged
AvailabilityChanged
ItemBecameUnavailable
FulfillmentNodeSuspended
```

Avoid technical names such as:

```text
UpdateMongoDocument
RedisRefreshRequested
ExecuteSqlCommand
```

---

# 11. Event Envelope

A common event envelope should carry technical metadata.

Conceptually:

```json
{
  "eventId": "...",
  "eventType": "PriceChanged",
  "eventVersion": 1,
  "occurredAtUtc": "...",
  "correlationId": "...",
  "causationId": "...",
  "producer": "Yunu.Commerce.Pricing",
  "payload": {}
}
```

Exact serialization contracts will be defined during implementation.

---

# 12. EventId

Every Integration Event must have a unique:

```text
EventId
```

It supports:

```text
deduplication
tracing
auditing
replay diagnostics
```

---

# 13. CorrelationId

CorrelationId connects operations across service boundaries.

Example:

```text
HTTP Request
    │
    ▼
Catalog Command
    │
    ▼
ProductCreated
    │
    ▼
Search Consumer
    │
    ▼
Elasticsearch
```

The same correlation context should be propagated where practical.

---

# 14. CausationId

CausationId identifies the event/message/request that caused another message.

Conceptually:

```text
Event A
  │
  ▼
Event B

Event B.CausationId = Event A.EventId
```

This improves distributed diagnostics.

---

# 15. Event Version

Every public Integration Event contract must support version evolution.

Example:

```text
ProductCreated v1
ProductCreated v2
```

Breaking changes must not silently reuse the same contract.

---

# 16. Schema Evolution

Prefer backward-compatible evolution.

Safe examples often include:

```text
adding optional fields
adding metadata
adding new event types
```

Potentially breaking changes include:

```text
renaming required fields
changing field meaning
changing identifier semantics
removing required fields
changing numeric units
```

Breaking changes require explicit versioning.

---

# 17. Event Contract Ownership

The producing Bounded Context owns the semantic meaning of its Integration Events.

Consumers must not force producer internal models into their own assumptions.

---

# 18. Contract Assemblies

Integration contracts should live in dedicated projects/packages when sharing is justified.

Example:

```text
Yunu.Commerce.Contracts
```

or context-specific contracts:

```text
Yunu.Commerce.Catalog.Contracts
Yunu.Commerce.Pricing.Contracts
```

These projects must contain contracts only.

They must not contain Domain behavior.

---

# 19. No Shared Domain Assemblies

Forbidden:

```text
Pricing.Application
    references
Catalog.Domain
```

Cross-context integration uses:

```text
contract
event
API
projection
```

not another context's Aggregate.

---

# 20. Transactional Outbox

Reliable publication uses the Transactional Outbox pattern.

Conceptually:

```text
Application Command
      │
      ▼
Domain Change
      │
      ├── Persist Aggregate
      └── Persist Outbox Event
             │
             ▼
       SAME TRANSACTION
```

Then:

```text
Outbox Publisher
      │
      ▼
Kafka
```

This avoids the dual-write problem.

---

# 21. Dual-Write Problem

Forbidden architecture:

```text
Save database
     │
     ▼
Publish Kafka
```

without reliability coordination.

Failure example:

```text
Database commit succeeds
Kafka publish fails
```

The business state changes but downstream systems never receive the event.

Outbox solves this class of failure.

---

# 22. Outbox Record

Conceptual Outbox data:

```text
OutboxMessage

Id
EventType
EventVersion
Payload
OccurredAtUtc
CorrelationId
ProcessedAtUtc
RetryCount
```

Exact storage belongs to each producing Infrastructure module.

---

# 23. Outbox Publisher

The Outbox Publisher is an Infrastructure/background process.

Responsibilities:

```text
read unpublished Outbox messages
publish to Kafka
mark successful publication
retry transient failures
emit telemetry
```

It must not contain business rules.

---

# 24. Inbox Pattern

Consumers requiring strong duplicate protection use an Inbox.

Conceptually:

```text
Kafka Event
    │
    ▼
Inbox Check
    │
    ├── Already processed → ignore
    │
    └── New
          │
          ▼
      Application Handler
          │
          ▼
      Mark processed
```

---

# 25. At-Least-Once Delivery

The architecture assumes that asynchronous messages may be delivered more than once.

Therefore:

> Consumers must be idempotent.

Do not design business correctness around exactly-once assumptions across distributed boundaries.

---

# 26. Idempotency

Potential idempotency mechanisms include:

```text
EventId
MessageId
SourceVersion
IdempotencyKey
Inbox
business uniqueness constraint
```

The appropriate mechanism depends on the operation.

---

# 27. Absolute vs Delta Events

This distinction is especially important for Availability.

Absolute:

```text
Set quantity to 10
```

Delta:

```text
Decrease quantity by 1
```

Absolute updates are naturally easier to make idempotent.

Delta events require stronger duplicate and ordering protection.

---

# 28. Ordering

Some event streams require ordering per business entity.

Examples:

```text
Availability by SkuId + FulfillmentNodeId
Price changes by OfferId + Scope
Offer lifecycle by OfferId
```

Kafka partition keys should reflect required ordering boundaries.

---

# 29. Kafka Partition Key

Potential keys include:

```text
ProductId
SellerId
OfferId
PriceId
SkuId + FulfillmentNodeId
FulfillmentNodeId
```

Partition strategy belongs to Integration Infrastructure.

It must be based on ordering and throughput requirements.

---

# 30. Kafka Topics

Topics should represent stable integration streams.

Possible initial examples:

```text
yunu.catalog.events
yunu.sellers.events
yunu.offers.events
yunu.pricing.events
yunu.availability.events
yunu.fulfillment.events
```

The final topology must be documented through ADRs.

---

# 31. Topic Granularity

Avoid automatically creating one topic per event type.

Also avoid putting every event in one global topic.

Topic design should consider:

```text
ownership
ordering
retention
consumer patterns
throughput
security
operational isolation
```

---

# 32. Consumer Groups

Independent capabilities use independent Consumer Groups.

Example:

```text
PriceChanged
      │
      ▼
Kafka
      │
      ├── Search Consumer Group
      ├── Cache Consumer Group
      └── Analytics Consumer Group
```

Each group processes the event independently.

---

# 33. Retry

Transient failures should be retried according to explicit policy.

Examples:

```text
temporary database timeout
temporary carrier outage
temporary search outage
temporary external API failure
```

Retries must be bounded.

---

# 34. Retry Safety

Never retry blindly when an operation is not idempotent.

Before enabling retry, determine:

```text
Can this operation safely execute twice?
```

If not, introduce idempotency protection first.

---

# 35. Exponential Backoff

Transient retries should generally use controlled backoff.

Conceptually:

```text
retry 1
wait
retry 2
wait longer
retry 3
```

Exact timing belongs to configuration.

---

# 36. Dead-Letter Handling

Messages that repeatedly fail require controlled failure handling.

Potential strategy:

```text
Main Topic
    │
    ▼
Consumer
    │
    ▼
Retries exhausted
    │
    ▼
Dead-Letter Topic / Failure Store
```

DLQ is not a garbage bin.

Every DLQ requires an operational recovery procedure.

---

# 37. Poison Messages

A poison message is a message that consistently fails because of:

```text
invalid schema
invalid business data
unsupported version
mapping defect
corrupted payload
```

Poison messages should not block an entire partition indefinitely.

---

# 38. Replay

The architecture must support controlled event replay.

Replay scenarios include:

```text
rebuild Search
rebuild Redis projection
recover consumer state
fix projection bug
reprocess historical integration
```

Consumers must be designed with replay behavior in mind.

---

# 39. Event Retention

Kafka retention must follow the role of each topic.

Potential requirements include:

```text
short operational retention
longer replay window
compacted current-state stream
audit stream
```

The final policy belongs to platform configuration.

---

# 40. Log Compaction

Compacted topics may be useful for current-state streams where the latest value per key matters.

Potential candidate:

```text
selected reference-data projections
```

Do not use compaction automatically for every business event stream.

---

# 41. Synchronous API Boundaries

REST APIs expose Application use cases.

Conceptually:

```text
Client
  │
  ▼
ASP.NET Core Host
  │
  ▼
Application
  │
  ▼
Domain
```

Controllers/endpoints must remain thin.

---

# 42. API Contracts

External API contracts must not expose Domain Entities directly.

Use explicit:

```text
Request DTO
Response DTO
```

Domain models remain internal.

---

# 43. API Versioning

Public or independently consumed APIs should support explicit versioning when required.

Example:

```text
/api/v1/products
```

Version strategy should be standardized before external consumers depend on it.

---

# 44. API Idempotency

Write APIs that may be retried by clients should support idempotency where business impact justifies it.

Potential header/concept:

```text
Idempotency-Key
```

Examples:

```text
product import
seller synchronization
price update
AI enrichment request
```

---

# 45. API Error Contract

APIs should return canonical error responses.

Potential structure:

```text
code
message
traceId
details
```

Infrastructure exceptions must not leak to clients.

---

# 46. Problem Details

ASP.NET Core Problem Details is a strong candidate for standardized HTTP errors.

Business error codes should remain stable and documented.

---

# 47. API Gateway

A production deployment may use an API Gateway such as:

```text
Azure API Management
```

Potential responsibilities:

```text
authentication enforcement
rate limiting
routing
API version exposure
request policies
observability
external API governance
```

Gateway policy must not contain core Domain rules.

---

# 48. Internal vs External APIs

The architecture should distinguish:

```text
public/customer APIs
partner APIs
seller APIs
internal service APIs
administrative APIs
```

Security and compatibility requirements differ.

---

# 49. Service-to-Service Authentication

Internal service calls should use workload identities where possible.

Potential Azure approach:

```text
Managed Identity
Microsoft Entra ID
OAuth 2.0
```

Avoid long-lived shared secrets.

---

# 50. External Authentication

External consumers may use:

```text
OAuth 2.0
OIDC
client credentials
signed API keys where justified
```

The exact strategy belongs to Security Architecture.

---

# 51. Anti-Corruption Layer

Every significant external business model should be translated before entering the canonical system.

Conceptually:

```text
External Model
      │
      ▼
Adapter
      │
      ▼
Anti-Corruption Mapping
      │
      ▼
Canonical Command / Contract
      │
      ▼
Application
```

---

# 52. Why Anti-Corruption Layers Matter

External systems may represent:

```text
Product
SKU
Seller
Price
Stock
Branch
Freight
```

differently.

Yunu.Commerce must not inherit those inconsistencies.

The ACL protects the canonical language.

---

# 53. External DTOs

Provider-specific DTOs belong to Integration/Infrastructure adapters.

Forbidden:

```text
Domain Entity
    contains
ExternalErpProductDto
```

Forbidden:

```text
Domain Service
    accepts
MarketplaceSkuResponse
```

---

# 54. Canonical Integration Model

Adapters translate external data into canonical inputs.

Example:

```text
ERP Product
      │
      ▼
ErpProductAdapter
      │
      ▼
CreateOrUpdateProductCommand
      │
      ▼
Catalog Application
```

---

# 55. External Product Integration

Potential flow:

```text
ERP / PIM / Marketplace
        │
        ▼
Integration Adapter
        │
        ▼
Canonical Product Command
        │
        ▼
Catalog
        │
        ▼
ProductChanged
        │
        ▼
Kafka
```

---

# 56. External Seller Integration

Potential flow:

```text
Marketplace Seller Feed
        │
        ▼
Seller Adapter
        │
        ▼
Canonical Seller Command
        │
        ▼
Sellers Context
```

---

# 57. External Offer Integration

Potential flow:

```text
Marketplace Offer
        │
        ▼
Offer Adapter
        │
        ▼
Canonical Offer Command
        │
        ▼
Offers Context
```

---

# 58. External Price Integration

Potential flow:

```text
ERP / Pricing Engine / Seller
        │
        ▼
Pricing Adapter
        │
        ▼
Canonical Price Command
        │
        ▼
Pricing Context
```

---

# 59. External Availability Integration

Potential flow:

```text
WMS / ERP / Store / Seller
        │
        ▼
Availability Adapter
        │
        ▼
Canonical Availability Update
        │
        ▼
Availability Context
```

Source version and update semantics must be preserved.

---

# 60. External Fulfillment Integration

Potential flow:

```text
ERP / WMS / Store Master
        │
        ▼
Fulfillment Adapter
        │
        ▼
Canonical Fulfillment Command
        │
        ▼
Fulfillment Context
```

---

# 61. Freight Provider Integration

Conceptually:

```text
Freight Application
       │
       ▼
CarrierQuotationPort
       │
       ├── Carrier A Adapter
       ├── Carrier B Adapter
       └── Logistics Platform Adapter
```

Carrier DTOs never enter Freight Domain.

---

# 62. AI Provider Integration

AI providers must also follow Hexagonal Architecture.

Conceptually:

```text
AI Application
      │
      ▼
GenerativeAiPort
      │
      ├── Azure AI Adapter
      └── Google AI Adapter
```

Provider switching must not require redesigning Catalog Domain.

Detailed behavior belongs to `docs/ai/ai-architecture.md`.

---

# 63. Search Integration

Search is updated through projections.

Conceptually:

```text
Catalog Events
Offer Events
Price Events
Availability Events
        │
        ▼
Kafka
        │
        ▼
Search Projection Consumers
        │
        ▼
Elasticsearch
```

Search must not query every Domain database at request time.

---

# 64. Redis Projection Integration

Redis may also be maintained from events.

Example:

```text
AvailabilityChanged
        │
        ▼
Availability Cache Consumer
        │
        ▼
Redis
```

This allows low-latency reads without making Redis authoritative.

---

# 65. Projection Consumers

Projection consumers are Integration/Application components.

They may:

```text
consume events
load required projection state
update Elasticsearch
update Redis
update specialized read stores
```

They must remain idempotent.

---

# 66. Projection Lag

Because projections are asynchronous:

```text
Canonical State
≠ always instantly equal to
Projection State
```

The architecture accepts bounded eventual consistency.

Projection lag must be observable.

---

# 67. Reconciliation

Every critical external integration should support reconciliation.

Conceptually:

```text
Real-time Events
       +
Periodic Snapshot
       │
       ▼
Reconciliation
       │
       ▼
Drift Detection
       │
       ▼
Correction
```

This is particularly important for:

```text
Catalog
Offers
Pricing
Availability
Fulfillment
```

---

# 68. Integration Snapshot

Snapshots may be used when an external system cannot guarantee every incremental change.

Large snapshots should be processed through dedicated workers and bulk-safe Application paths.

---

# 69. Import Files

Large imports may use:

```text
CSV
JSON
Parquet
provider-specific files
```

Conceptually:

```text
Upload
  │
  ▼
Object Storage
  │
  ▼
Import Worker
  │
  ▼
Adapter / ACL
  │
  ▼
Application
```

---

# 70. File Integration

Do not process large files entirely inside synchronous HTTP requests.

Use background processing and expose import status.

---

# 71. Import Job

Potential model:

```text
ImportJob

ImportJobId
Source
Type
Status
StartedAtUtc
CompletedAtUtc
ProcessedCount
FailedCount
CorrelationId
```

This is operational/Application state, not necessarily a Domain Aggregate.

---

# 72. Bulk Processing

Bulk integrations must avoid:

```text
one database transaction for millions of records
unbounded memory
unbounded parallelism
```

Use:

```text
chunking
bounded concurrency
checkpointing
idempotency
backpressure
```

---

# 73. Backpressure

Consumers must not overwhelm downstream dependencies.

Potential mechanisms include:

```text
bounded consumer concurrency
Kafka consumer configuration
rate limiting
bulk writes
buffering
circuit breaking
```

---

# 74. External Rate Limits

Adapters must respect provider rate limits.

Examples:

```text
marketplace APIs
AI providers
carrier APIs
postal-code providers
```

Rate limiting is an Infrastructure concern.

---

# 75. Resilience

External integrations should use resilience policies appropriate to failure type.

Potential patterns:

```text
Timeout
Retry
Circuit Breaker
Bulkhead
Fallback
Rate Limiting
```

In .NET, resilience implementation may use the current Microsoft/Polly ecosystem.

The Domain remains unaware of these libraries.

---

# 76. Timeout Budget

Every synchronous dependency must have an explicit timeout.

Do not allow infinite or framework-default waits to define production behavior.

End-to-end request budgets must account for nested calls.

---

# 77. Circuit Breaker

Circuit Breakers protect the platform from repeatedly calling unhealthy dependencies.

Potential candidates:

```text
carrier APIs
external marketplaces
AI providers
postal-code services
legacy systems
```

Circuit state is operational, not Domain state.

---

# 78. Fallback

Fallback must preserve business correctness.

Examples:

```text
AI provider unavailable
→ use another configured AI provider

Redis unavailable
→ query canonical store where acceptable

Carrier unavailable
→ return remaining valid carriers
```

Never fabricate business data as a fallback.

---

# 79. Integration Failure Classification

Failures should be classified.

Potential categories:

```text
Transient
Permanent
Validation
Authentication
Authorization
RateLimit
Timeout
Unavailable
Conflict
UnsupportedContract
```

This improves retry and observability decisions.

---

# 80. Observability

Every integration boundary must emit telemetry.

At minimum:

```text
trace
metrics
structured logs
correlation identifiers
dependency duration
success/failure
retry count
```

OpenTelemetry is the preferred instrumentation standard.

---

# 81. Distributed Tracing

Trace context should propagate through:

```text
HTTP
Kafka message metadata
background jobs
external calls
```

A user/API request should be traceable through downstream asynchronous work where practical.

---

# 82. Structured Logging

Logs should contain structured fields such as:

```text
CorrelationId
EventId
MessageType
BoundedContext
Operation
ExternalProvider
Duration
Result
```

Avoid relying on unstructured text-only logs.

---

# 83. Sensitive Data in Logs

Never log:

```text
access tokens
passwords
private keys
full secrets
sensitive personal data
unnecessary AI prompt content
```

Logging policies must include redaction.

---

# 84. Metrics

Important integration metrics include:

```text
Kafka consumer lag
event throughput
failed messages
DLQ count
Outbox backlog
Inbox duplicate count
API latency
external dependency latency
timeout rate
retry rate
circuit breaker state
projection lag
```

---

# 85. Health Checks

Integration dependencies should expose health/readiness information where useful.

Potential dependencies:

```text
Kafka
MongoDB
SQL
Redis
Elasticsearch
external critical APIs
```

Do not make every optional external provider a hard liveness dependency.

---

# 86. Liveness vs Readiness

Liveness:

```text
Is the process alive?
```

Readiness:

```text
Can the process currently serve its required workload?
```

These checks must not be confused.

---

# 87. Contract Testing

External adapters should have contract tests.

Examples:

```text
Marketplace API contract
Carrier API contract
AI provider contract
Kafka event schema contract
```

This catches integration drift earlier.

---

# 88. Consumer Contract Testing

Important Integration Events should be tested for compatibility between producer and consumers.

Breaking schema changes must be detected before deployment.

---

# 89. Integration Tests

Integration tests should verify real infrastructure behavior using Testcontainers where practical.

Candidates:

```text
Kafka
MongoDB
SQL
Redis
Elasticsearch
```

External SaaS providers may use:

```text
sandbox environments
mock servers
recorded fixtures where appropriate
```

---

# 90. Architecture Tests

Architecture tests should enforce:

```text
Domain cannot reference Kafka

Domain cannot reference HTTP clients

Domain cannot reference external provider SDKs

Application cannot depend directly on concrete provider adapters

Bounded Context Domain projects cannot reference each other

Infrastructure implements Application/Domain ports
```

---

# 91. Security at Integration Boundaries

Every integration must define:

```text
authentication
authorization
transport encryption
secret storage
network exposure
rate limits
auditability
```

External integration credentials belong in secure secret management.

---

# 92. Secret Management

Production secrets should use mechanisms such as:

```text
Azure Key Vault
Managed Identity
Workload Identity
```

Secrets must not be stored in:

```text
appsettings.json committed to Git
source code
Docker images
Markdown documentation
```

---

# 93. API Network Security

Production APIs may use:

```text
private endpoints
API Management
WAF
network policies
TLS
service identity
```

Exact deployment topology belongs to Platform Architecture.

---

# 94. Kafka Security

Production Kafka should use appropriate:

```text
authentication
authorization
TLS
topic ACLs
consumer-group permissions
```

Each workload should receive only the permissions it requires.

---

# 95. Schema Registry

A Schema Registry may be introduced when event-contract governance requires it.

Potential formats include:

```text
JSON Schema
Avro
Protobuf
```

Initial implementation may use JSON contracts if simplicity is more valuable.

The choice must be documented by ADR.

---

# 96. JSON Events

JSON is a reasonable initial Integration Event format because it is:

```text
human-readable
easy to debug
widely supported
simple for early development
```

At larger scale, schema-controlled binary formats may be evaluated.

---

# 97. Event Compatibility

Consumers should tolerate additive fields they do not understand.

Do not implement fragile deserialization that fails merely because a producer added an optional field.

---

# 98. Event Time

Integration Events should include:

```text
OccurredAtUtc
```

Processing time and business occurrence time are not necessarily the same.

---

# 99. UTC

System integration timestamps must use UTC.

Local time conversion happens explicitly at presentation or business-rule boundaries.

---

# 100. Integration IDs

External identifiers must never silently replace canonical IDs.

Example:

```text
ExternalSkuId
    maps to
SkuId
```

Both identities may need to be retained.

---

# 101. External Mapping Store

Integration adapters may maintain mappings such as:

```text
External System
External Entity Type
External Id
Canonical Id
```

Mapping ownership should be explicit.

---

# 102. Integration Versioning

External provider versions should be isolated inside adapters.

Example:

```text
MarketplaceAdapterV1
MarketplaceAdapterV2
```

The canonical Application contract should remain stable where possible.

---

# 103. Adapter Replacement

A major architectural goal is replaceability.

Example:

```text
Azure AI
    ↓
Google AI
```

or:

```text
Carrier A
    ↓
Carrier B
```

or:

```text
Legacy ERP
    ↓
New ERP
```

These changes should primarily affect adapters, not Domain behavior.

---

# 104. Internal Orchestration

Some use cases require data from multiple Bounded Contexts.

Example:

```text
Customer Product Detail

Catalog
+
Offer
+
Price
+
Availability
+
Freight summary
```

This should be composed through:

```text
Application orchestration
read projection
API composition
```

not direct Domain references.

---

# 105. API Composition

For low-volume or strongly fresh data, an API composition layer may query multiple services.

However, excessive synchronous fan-out increases:

```text
latency
failure probability
coupling
```

Prefer projections for high-volume customer-facing reads.

---

# 106. Customer Commerce Projection

A customer-facing projection may consume:

```text
Product events
Offer events
Price events
Availability events
Fulfillment events
```

and build a denormalized commerce view.

This is often preferable to synchronous fan-out for product listing pages.

---

# 107. Saga / Process Manager

Long-running workflows may eventually require a Saga or Process Manager.

Potential future examples:

```text
Seller onboarding
Complex product onboarding
Multi-step AI enrichment
Order orchestration
```

Do not introduce Saga infrastructure until a real multi-step business process requires it.

---

# 108. Choreography vs Orchestration

Choreography:

```text
Event
→ independent consumers react
```

Orchestration:

```text
Coordinator
→ explicitly directs workflow steps
```

Use choreography for loosely coupled reactions.

Use orchestration when business process state must be controlled explicitly.

---

# 109. Event Storm Protection

A high-frequency Domain such as Availability can produce enormous event volume.

Do not publish unnecessary semantic noise.

Example:

```text
Quantity 100 → 99
```

may not require Search reindexing.

But:

```text
Quantity 1 → 0
```

may produce:

```text
ItemBecameUnavailable
```

Projection-specific filtering can reduce downstream load.

---

# 110. Event Aggregation

Where appropriate, Infrastructure/Application may batch or coalesce projection updates.

This must not change canonical business history.

---

# 111. Search Projection Flow

Conceptually:

```text
Catalog ───────┐
Offers ────────┤
Pricing ───────┤
Availability ──┤
Fulfillment ───┤
               ▼
             Kafka
               │
               ▼
       Search Projection
               │
               ▼
         Elasticsearch
```

---

# 112. AI Enrichment Integration Flow

Conceptually:

```text
Catalog
   │
   ▼
AI Enrichment Request
   │
   ▼
AI Application
   │
   ▼
AI Provider Port
   │
   ├── Azure AI
   └── Google AI
   │
   ▼
Structured Proposal
   │
   ▼
Validation / Approval
   │
   ▼
Catalog Command
```

AI never writes directly to Catalog storage.

---

# 113. AI Tool Integration

Future AI Agents may use application tools such as:

```text
FindProduct
CreateProductDraft
ValidateSku
SearchCatalog
GetSeller
GetCurrentPrice
GetAvailability
```

Tools must call Application contracts.

Agents must not receive direct database clients.

---

# 114. Integration with Legacy Systems

Legacy integration must be isolated.

Conceptually:

```text
Legacy System
      │
      ▼
Legacy Adapter
      │
      ▼
Anti-Corruption Layer
      │
      ▼
Canonical Yunu Contract
```

Legacy terminology must not become the new Domain language by accident.

---

# 115. Integration Ownership

Each integration must have an explicit owner.

Examples:

```text
Catalog integration
→ Catalog capability

Price integration
→ Pricing capability

Availability integration
→ Availability capability

Carrier integration
→ Freight capability

AI provider integration
→ AI capability
```

Avoid one giant "Integration Service" containing all business integrations.

---

# 116. Integration Project Structure

A possible .NET structure is:

```text
src/

├── BuildingBlocks/
│   ├── Messaging/
│   ├── Integration/
│   └── Observability/
│
├── Catalog/
│   └── Catalog.Infrastructure/
│       └── Integrations/
│
├── Pricing/
│   └── Pricing.Infrastructure/
│       └── Integrations/
│
├── Availability/
│   └── Availability.Infrastructure/
│       └── Integrations/
│
└── Freight/
    └── Freight.Infrastructure/
        └── Carriers/
```

Exact projects will follow `06-solution-structure.md`.

---

# 117. Building Blocks

Shared technical building blocks may include:

```text
EventEnvelope
Outbox abstractions
Inbox abstractions
Correlation
Idempotency
Messaging serialization
Observability helpers
```

Building Blocks must not become a dumping ground for business logic.

---

# 118. Shared Kernel Warning

Do not place Domain concepts into generic shared integration libraries merely to avoid duplication.

A tiny amount of duplication is often safer than accidental cross-context coupling.

---

# 119. Configuration

Integration configuration should use strongly typed options.

Examples:

```text
KafkaOptions
CarrierOptions
MarketplaceOptions
AiProviderOptions
```

Secrets remain outside ordinary configuration files.

---

# 120. Feature Flags

Feature flags may help migrate integrations.

Examples:

```text
UseNewCatalogAdapter
EnableGoogleAiProvider
EnableNewCarrier
EnableSearchProjectionV2
```

Feature flags must not permanently replace architecture decisions.

---

# 121. Deployment Independence

Where practical, consumers and adapters should support independent deployment.

At minimum, their boundaries should allow future extraction without rewriting the Domain.

The first implementation may remain a Modular Monolith plus workers.

---

# 122. Modular Monolith First

Yunu.Commerce does not need to begin as dozens of microservices.

The architecture can start as:

```text
Modular Monolith
+
Background Workers
+
Kafka
+
Independent data ownership
```

while preserving boundaries that permit future extraction.

---

# 123. Microservice Extraction

A Bounded Context becomes a strong microservice candidate when there is a concrete reason such as:

```text
independent scaling
independent deployment cadence
team ownership
fault isolation
technology requirements
very different workload
```

Do not extract services merely because the architecture uses DDD.

---

# 124. Availability Extraction Candidate

Availability may become an early extraction candidate because of:

```text
high write throughput
high read throughput
Kafka-heavy integration
independent scaling
Redis projections
```

But this should follow measurements.

---

# 125. Search Extraction Candidate

Search projection and query workloads may also be independently scalable.

Search remains a projection capability, not a business source of truth.

---

# 126. AI Extraction Candidate

AI processing may benefit from independent workers because:

```text
provider latency is high
cost controls differ
rate limits differ
work may be asynchronous
model workloads vary
```

The Domain architecture should allow this without changing Catalog.

---

# 127. Development Environment

Local development should eventually provide:

```text
Kafka
MongoDB
Relational DB
Redis
Elasticsearch
```

through Docker Compose.

External provider adapters may use:

```text
fake adapters
sandbox providers
development cloud accounts
```

---

# 128. Fake Adapters

Every major external port should have a simple fake/test adapter where useful.

Examples:

```text
FakeGenerativeAiProvider
FakeCarrierProvider
FakePostalCodeResolver
```

This allows the application to run without every external dependency.

---

# 129. Integration Testing Pyramid

Testing should include:

```text
Domain unit tests

Application tests

Adapter contract tests

Infrastructure integration tests

End-to-end tests for critical flows
```

Do not depend exclusively on slow end-to-end environments.

---

# 130. Initial Integration Flow

The first complete vertical integration should be:

```text
Create Product
      │
      ▼
Catalog Application
      │
      ▼
Catalog Domain
      │
      ▼
MongoDB
      │
      ▼
Outbox
      │
      ▼
Kafka
      │
      ▼
Search Projection
      │
      ▼
Elasticsearch
```

Then:

```text
AI Enrichment
      │
      ▼
Catalog validated update
      │
      ▼
same event/projection pipeline
```

This validates the architecture end-to-end.

---

# 131. Initial Kafka Events

The first event contracts should remain minimal.

Potential starting set:

```text
ProductCreated
ProductUpdated
SkuCreated
SkuUpdated
```

Additional events should be introduced only when a real consumer exists.

---

# 132. Initial Consumer

The first consumer should be the Search Projection Worker.

It validates:

```text
Kafka
Outbox
event contracts
idempotency
Elasticsearch
observability
```

without requiring all commerce contexts to be implemented.

---

# 133. Second Integration Slice

After Catalog + Search:

```text
Pricing
    │
    ▼
PriceChanged
    │
    ▼
Kafka
    │
    ▼
Search Projection
```

This proves multi-context projection composition.

---

# 134. Third Integration Slice

Then:

```text
Availability
    │
    ▼
AvailabilityChanged
    │
    ▼
Kafka
    │
    ├── Redis Projection
    └── Search Projection
```

This validates the high-frequency event path.

---

# 135. Architecture Decisions Requiring ADRs

Important Integration ADRs include:

```text
Use Kafka for asynchronous integration

Use Transactional Outbox

Use Inbox for idempotent consumers

Use JSON Integration Events initially

Kafka topic strategy

Event versioning strategy

REST as initial synchronous API style

Use Anti-Corruption Layers for external systems

Start as Modular Monolith with extractable contexts

Use OpenTelemetry for distributed observability
```

---

# 136. Copilot Implementation Rules

When GitHub Copilot generates integration code, it must:

```text
respect Bounded Context boundaries

never reference another context's Domain project

never integrate through another context's database

never place Kafka code in Domain

never place HTTP provider code in Domain

use ports/interfaces before concrete adapters

translate external DTOs through adapters

implement idempotency for event consumers

use Outbox for reliable publication

propagate correlation metadata

use cancellation tokens for I/O

use async I/O

avoid fire-and-forget processing

add tests for contracts and handlers

avoid speculative abstractions not documented here
```

---

# 137. Forbidden Integration Patterns

The following are forbidden unless explicitly approved by ADR:

```text
Shared database integration

Cross-context SQL joins

Domain-to-Domain project references

Kafka client inside Domain

External SDK inside Domain

Carrier DTO inside Freight Domain

AI provider DTO inside Catalog Domain

Direct Elasticsearch writes from Catalog Domain

Direct Redis writes from Availability Domain

Publishing Kafka before database commit without Outbox

Infinite retries

Unbounded parallelism

Swallowing integration exceptions silently

Using DLQ without recovery procedure
```

---

# 138. Architecture Questions Before Implementation

Before implementing concrete integration infrastructure, explicitly decide:

```text
Which Kafka distribution will be used locally?

Which Kafka service will be used in cloud?

Will Azure Event Hubs Kafka compatibility be considered?

What is the initial topic strategy?

What event serialization format is used?

Do we introduce Schema Registry immediately?

How is Outbox publication hosted?

How is Inbox persisted?

What is the default retry policy?

What is the DLQ strategy?

How are failed messages replayed?

What API versioning strategy is used?

Which API Gateway is used in Azure?

How is service-to-service authentication implemented?

How are external provider secrets stored?

What are the first external Catalog sources?

What are the first Freight providers?

Which AI provider is implemented first?

What OpenTelemetry backend is used?
```

---

# 139. Initial Implementation Sequence

Recommended sequence:

```text
1. Define Integration Event base contracts

2. Define Event Envelope

3. Define correlation abstractions

4. Implement Outbox persistence abstraction

5. Implement Kafka producer adapter

6. Implement Outbox Publisher Worker

7. Implement Kafka consumer base infrastructure

8. Implement Inbox/idempotency support

9. Create ProductCreated integration event

10. Create Search Projection Consumer

11. Index Product in Elasticsearch

12. Add OpenTelemetry traces and metrics

13. Add retry and DLQ behavior

14. Add AI provider integration

15. Add Pricing event flow

16. Add Availability event flow

17. Add external enterprise adapters
```

---

# 140. Core Rule

The core Integration Architecture rule is:

> Integrate through explicit business contracts, never through implementation details.

That means:

```text
API instead of shared database

Event instead of hidden coupling

Port instead of provider SDK

Adapter instead of external model leakage

Outbox instead of unsafe dual-write

Idempotency instead of exactly-once assumptions
```

---

# 141. Final Principle

Yunu.Commerce Integration Architecture must remain:

```text
explicit
loosely coupled
event-driven
idempotent
observable
resilient
versionable
replayable
provider-independent
cloud-adaptable
DDD-aligned
Hexagonal
eventually consistent where appropriate
```

Kafka may change.

External systems may change.

AI providers may change.

Carrier providers may change.

Cloud services may change.

The Bounded Context contracts and business semantics must remain protected.
