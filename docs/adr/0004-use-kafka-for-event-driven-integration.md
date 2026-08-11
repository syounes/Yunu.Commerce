# ADR-0004: Use Kafka for Event-Driven Integration

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Asynchronous integration between Bounded Contexts and platform consumers

## 1. Context

Yunu.Commerce contains independently owned Bounded Contexts with different workloads and consistency requirements:

```text
Catalog
Sellers
Offers
Pricing
Availability
Fulfillment
Freight
```

The platform also contains derived capabilities such as:

```text
Search projections
Redis projections
AI processing
Integration workers
Analytics consumers
```

Changes occurring in one context frequently need to be propagated to several independent consumers.

Examples:

```text
ProductCreated
PriceChanged
AvailabilityChanged
OfferActivated
FulfillmentNodeChanged
```

Direct synchronous service-to-service communication for every propagation would increase runtime coupling, latency and failure propagation.

Yunu.Commerce therefore requires a durable asynchronous integration backbone.

## 2. Decision

Yunu.Commerce will use Apache Kafka as the primary event-streaming platform for asynchronous integration.

Conceptually:

```text
Bounded Context
      │
      ▼
Local Transaction
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
      ├── Search Projection
      ├── Redis Projection
      ├── AI Worker
      ├── Integration Consumer
      └── Future Analytics Consumer
```

Kafka is an Infrastructure concern.

Domain projects must not reference Kafka libraries.

## 3. Why Kafka

Kafka is selected because Yunu.Commerce requires capabilities such as:

```text
high-throughput event streams
durable event retention
independent consumer groups
partition-based ordering
event replay
horizontal consumer scaling
multiple independent consumers
integration decoupling
```

These characteristics are particularly relevant to commerce workloads such as Catalog, Pricing and Availability.

## 4. Event-Driven Architecture

Kafka supports the Event-Driven Architecture defined for Yunu.Commerce.

The intended flow is:

```text
Business Operation
      │
      ▼
Domain Change
      │
      ▼
Domain Event
      │
      ▼
Application Mapping
      │
      ▼
Integration Event
      │
      ▼
Outbox
      │
      ▼
Kafka
```

Domain Events and Integration Events remain separate concepts.

## 5. Kafka Is Not the Domain

Kafka-specific concepts must remain outside Domain projects.

Forbidden:

```text
Catalog.Domain
    │
    ▼
Confluent.Kafka
```

Correct:

```text
Catalog.Domain
      │
      ▼
Catalog.Application
      │
      ▼
Messaging Port
      │
      ▼
Kafka Adapter
```

## 6. Integration Events

Kafka carries Integration Events.

Examples:

```text
ProductCreated
ProductUpdated
SkuCreated
SkuUpdated

SellerActivated
SellerUpdated

OfferActivated
OfferUpdated

PriceChanged

AvailabilityChanged
ItemBecameUnavailable

FulfillmentNodeChanged
```

Events represent facts that already happened.

## 7. Event Naming

Integration Events use business-oriented past-tense names.

Preferred:

```text
ProductCreated
PriceChanged
AvailabilityChanged
```

Avoid:

```text
UpdateProductDatabase
RefreshRedis
ExecutePriceUpdate
```

Events communicate business facts, not infrastructure commands.

## 8. Event Envelope

Kafka events should use a common provider-neutral envelope.

Conceptually:

```json
{
  "eventId": "...",
  "eventType": "ProductCreated",
  "eventVersion": 1,
  "occurredAtUtc": "...",
  "correlationId": "...",
  "causationId": "...",
  "producer": "Yunu.Commerce.Catalog",
  "payload": {}
}
```

The exact serialization contract will be implemented in shared messaging building blocks.

## 9. Event Identity

Every event must have a unique:

```text
EventId
```

It supports:

```text
deduplication
diagnostics
auditing
replay
traceability
```

## 10. Correlation

Messages should propagate:

```text
CorrelationId
CausationId
```

where applicable.

This allows tracing flows such as:

```text
HTTP Request
      │
      ▼
Create Product
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

## 11. Delivery Semantics

Yunu.Commerce assumes:

```text
at-least-once delivery
```

Consumers must therefore be idempotent.

The architecture must not depend on a distributed exactly-once guarantee for business correctness.

## 12. Consumer Idempotency

Potential mechanisms include:

```text
EventId
Inbox
Idempotency Key
Source Version
Business uniqueness constraints
```

The mechanism depends on the consumer and business operation.

## 13. Transactional Outbox

Kafka publication must use the Transactional Outbox pattern for reliable business event publication.

Conceptually:

```text
Database Transaction
│
├── Aggregate change
└── Outbox message
        │
        ▼
   COMMIT TOGETHER
```

Then:

```text
Outbox Publisher
      │
      ▼
Kafka
```

See ADR-0005.

## 14. No Unsafe Dual Writes

Forbidden:

```text
Save Product
      │
      ▼
Publish Kafka
```

as two unrelated operations.

Failure between the operations could leave downstream systems inconsistent.

## 15. Topic Strategy

Kafka topics should represent stable event streams and ownership boundaries.

Initial conceptual topics:

```text
yunu.catalog.events
yunu.sellers.events
yunu.offers.events
yunu.pricing.events
yunu.availability.events
yunu.fulfillment.events
```

Freight topics will be introduced when asynchronous Freight events are required.

## 16. Topic Granularity

Yunu.Commerce will not automatically create:

```text
one topic per event
```

and will not place every platform event into:

```text
one global topic
```

Topic design must consider:

```text
ownership
ordering
throughput
retention
security
consumer patterns
operational isolation
```

## 17. Partitioning

Partition keys must preserve required ordering boundaries.

Potential keys:

```text
Catalog
→ ProductId or SkuId

Offers
→ OfferId

Pricing
→ OfferId / Price scope

Availability
→ SkuId + FulfillmentNodeId

Fulfillment
→ FulfillmentNodeId
```

Exact key strategies must be validated against real event semantics.

## 18. Ordering

Kafka provides ordering within a partition, not globally across a topic.

Yunu.Commerce must never assume global event ordering unless explicitly engineered.

Business flows requiring ordering must use appropriate partition keys and source versions.

## 19. Availability Workload

Availability is expected to be one of the highest-volume event producers.

Potential flow:

```text
ERP / WMS / Store
       │
       ▼
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

Event design must avoid unnecessary downstream noise.

## 20. Semantic Events

Not every technical state update requires a broadly distributed event.

Example:

```text
Quantity 100 → 99
```

may not matter to Search.

But:

```text
Quantity 1 → 0
```

may produce a meaningful event such as:

```text
ItemBecameUnavailable
```

Consumers and event contracts should follow business semantics.

## 21. Consumer Groups

Independent capabilities use independent consumer groups.

Example:

```text
ProductUpdated
      │
      ▼
Kafka
      │
      ├── Search Consumer Group
      ├── Embedding Consumer Group
      └── Analytics Consumer Group
```

Each group processes the stream independently.

## 22. Consumer Scaling

Consumers in the same group may scale horizontally.

Effective parallelism is bounded by topic partitioning.

Partition counts must therefore consider expected throughput and future scaling.

## 23. Retry

Consumers may retry transient failures.

Examples:

```text
temporary MongoDB timeout
temporary Elasticsearch outage
temporary Redis outage
temporary network failure
```

Retries must be bounded and observable.

## 24. Retry Safety

Retries require idempotent behavior.

Before adding retry, answer:

```text
Can this operation safely execute more than once?
```

If not, idempotency must be implemented first.

## 25. Dead-Letter Handling

Messages that cannot be successfully processed after the defined policy may be routed to a Dead-Letter Topic or equivalent failure store.

Conceptually:

```text
Kafka Topic
    │
    ▼
Consumer
    │
    ▼
Retries exhausted
    │
    ▼
DLQ
```

DLQ usage requires a documented recovery/replay procedure.

## 26. Poison Messages

Invalid or permanently incompatible messages must not block a partition indefinitely.

Examples:

```text
invalid schema
unsupported event version
corrupted payload
invalid mapping
permanent business incompatibility
```

They must be isolated and observable.

## 27. Replay

Kafka retention enables controlled replay.

Potential use cases:

```text
rebuild Elasticsearch
rebuild Redis projection
recover a consumer
reprocess corrected projection logic
bootstrap a new consumer
```

Consumers must be designed with replay behavior in mind.

## 28. Projection Rebuild

Derived stores should be rebuildable from canonical data and/or retained events.

Example:

```text
Kafka Events
      │
      ▼
Search Projection Worker
      │
      ▼
New Elasticsearch Index
```

After validation, an alias may be switched to the rebuilt index.

## 29. Retention

Retention is configured according to stream purpose.

Factors include:

```text
replay requirements
storage cost
event volume
audit requirements
consumer recovery window
```

A single retention policy will not necessarily fit every topic.

## 30. Log Compaction

Compacted topics may be used when the latest state per key is the desired stream semantics.

Compaction will not be enabled automatically for ordinary business event streams.

## 31. Event Serialization

Initial Integration Events may use JSON because it provides:

```text
simplicity
readability
easy debugging
broad tooling support
```

Schema Registry and formats such as Avro or Protobuf may be evaluated later.

Any change must be documented through an ADR.

## 32. Schema Evolution

Event contracts must support version evolution.

Prefer backward-compatible changes such as:

```text
adding optional fields
adding metadata
```

Breaking changes require explicit versioning.

## 33. Consumer Compatibility

Consumers should tolerate additive fields they do not understand.

Fragile deserialization that fails because a producer added an optional field should be avoided.

## 34. Contract Ownership

The producing Bounded Context owns the semantic meaning of its events.

Consumers must not force producers to expose internal Aggregate representations.

## 35. No Aggregate Serialization

Do not publish entire Domain Aggregates as Kafka events by default.

Forbidden concept:

```text
Serialize Product Aggregate
→ Kafka
```

Prefer a focused Integration Event contract containing only meaningful integration data.

## 36. Search Integration

Kafka is the primary asynchronous path for Search projections.

Conceptually:

```text
Catalog ────────┐
Offers ─────────┤
Pricing ────────┤
Availability ───┤
Fulfillment ────┤
                ▼
              Kafka
                │
                ▼
       Search Projection
                │
                ▼
          Elasticsearch
```

## 37. Redis Integration

Kafka may update Redis projections.

Example:

```text
AvailabilityChanged
      │
      ▼
Kafka
      │
      ▼
Availability Projection Consumer
      │
      ▼
Redis
```

Redis remains derived state.

## 38. AI Integration

Kafka may trigger asynchronous AI workflows.

Examples:

```text
ProductEnrichmentRequested
EmbeddingGenerationRequested
DocumentProcessingRequested
```

AI consumers remain subject to rate limits, cost controls and idempotency.

## 39. Embedding Pipeline

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
AI Embedding Provider
      │
      ▼
Elasticsearch Vector Index
```

## 40. External Integration

Kafka may also decouple Yunu.Commerce from enterprise integration adapters.

Example:

```text
PriceChanged
      │
      ▼
Kafka
      │
      ▼
External Integration Consumer
      │
      ▼
Partner / ERP / Data Platform
```

Provider-specific logic remains in adapters.

## 41. Kafka Abstraction

Application code should depend on provider-neutral messaging abstractions where needed.

Possible abstraction:

```text
IIntegrationEventPublisher
```

However, normal business flows should rely on the Outbox rather than publishing directly from handlers.

## 42. Kafka Adapter

Concrete Kafka implementation belongs to Infrastructure.

Possible technology:

```text
Confluent.Kafka
```

The exact .NET client is an implementation detail.

## 43. Workers

Potential executable workers include:

```text
Outbox Publisher Worker
Search Projection Worker
Availability Projection Worker
Embedding Worker
Integration Worker
```

Workers are Hosts/composition roots.

## 44. Backpressure

Consumers must protect downstream systems from overload.

Mechanisms may include:

```text
bounded concurrency
consumer configuration
pause/resume
batch processing
bulk writes
rate limiting
```

## 45. Batch Consumption

Where appropriate, high-volume consumers may process messages in bounded batches.

Examples:

```text
Elasticsearch bulk indexing
MongoDB bulk operations
```

Batching must preserve required idempotency and failure semantics.

## 46. Observability

Kafka infrastructure must expose telemetry.

Important metrics include:

```text
producer throughput
producer failures
consumer throughput
consumer lag
processing latency
retry count
DLQ count
Outbox backlog
event age
```

## 47. Distributed Tracing

Trace metadata should propagate through Kafka headers/envelopes where appropriate.

OpenTelemetry is the preferred observability standard.

## 48. Structured Logging

Kafka processing logs should include:

```text
EventId
EventType
EventVersion
CorrelationId
ConsumerGroup
Topic
Partition
Offset
ProcessingResult
Duration
```

Sensitive payloads must not be logged indiscriminately.

## 49. Security

Production Kafka infrastructure must support:

```text
TLS
authentication
authorization
topic ACLs
consumer-group permissions
least privilege
secret/identity management
```

## 50. Cloud Independence

The Domain and Application layers must not depend on a specific Kafka hosting provider.

Kafka may be hosted through:

```text
managed Kafka service
self-managed Kafka
cloud-compatible Kafka offering
local Docker infrastructure
```

The cloud choice belongs to ADR-0009 and deployment configuration.

## 51. Local Development

Local development should support Kafka through containerized infrastructure.

Conceptually:

```text
Docker Compose
│
├── Kafka
├── MongoDB
├── Relational DB
├── Redis
└── Elasticsearch
```

The exact local Kafka distribution may change without affecting application architecture.

## 52. Testing

Kafka-related testing should include:

```text
event contract tests
producer integration tests
consumer integration tests
idempotency tests
retry tests
DLQ tests
replay tests
```

Testcontainers may be used for real infrastructure integration tests.

## 53. Architecture Tests

Architecture tests must verify:

```text
Domain projects do not reference Kafka packages.

Application projects do not depend on concrete Kafka clients.

Kafka consumers live outside Domain.

Kafka producers/adapters live in Infrastructure.

Bounded Contexts communicate through contracts rather than Domain references.
```

## 54. Initial Implementation

The first event-driven vertical slice should be:

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
Outbox Publisher
      │
      ▼
Kafka
      │
      ▼
Search Projection Worker
      │
      ▼
Elasticsearch
```

This validates the core asynchronous architecture end-to-end.

## 55. Initial Events

Start with a small event set:

```text
ProductCreated
ProductUpdated
SkuCreated
SkuUpdated
```

Do not create dozens of speculative events before consumers exist.

## 56. Second Event Slice

After Catalog:

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

## 57. Third Event Slice

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

This introduces the higher-throughput path.

## 58. Consequences

### Positive

```text
loose coupling
durable asynchronous integration
consumer independence
horizontal scaling
replay capability
high throughput
projection support
better failure isolation
future analytics integration
```

### Negative

```text
eventual consistency
operational complexity
consumer idempotency requirements
schema governance
partition planning
monitoring requirements
duplicate delivery handling
```

These tradeoffs are accepted.

## 59. Alternatives Considered

### Synchronous REST for All Integration

Rejected because it creates runtime coupling and poor fan-out characteristics for event propagation.

REST remains valid when an immediate response is required.

### Database Polling as Primary Integration

Rejected because databases should not become integration contracts.

Outbox polling is allowed as an implementation mechanism for reliable event publication.

### RabbitMQ as Primary Event Backbone

Not selected for the primary platform because Kafka's retained streams, replay and partitioned high-throughput model align strongly with Yunu.Commerce projection and Availability workloads.

RabbitMQ or another broker may still be considered for a future workload with different semantics.

### Cloud-Specific Messaging in Domain/Application

Rejected because it would weaken cloud portability and Hexagonal boundaries.

## 60. Copilot Rules

GitHub Copilot must:

```text
Never reference Kafka libraries from Domain.

Never publish Kafka directly from an Aggregate.

Never use Kafka as a replacement for Domain Events.

Separate Domain Events from Integration Events.

Use Transactional Outbox for reliable publication.

Assume at-least-once delivery.

Make consumers idempotent.

Propagate EventId and CorrelationId.

Use CancellationToken.

Use bounded retries.

Avoid unbounded consumer concurrency.

Do not swallow consumer failures.

Do not create topics casually without documented ownership.

Do not serialize full Aggregates as event contracts.

Keep Kafka configuration in Infrastructure/Host layers.

Add telemetry to producers and consumers.
```

## 61. Relationship to Other ADRs

This ADR depends on:

```text
ADR-0001
Use DDD, Clean Architecture and Hexagonal Architecture

ADR-0002
Bounded Context Strategy

ADR-0003
Database per Bounded Context
```

It directly informs:

```text
ADR-0005
Use Transactional Outbox

ADR-0006
Use Redis for Distributed Cache

ADR-0007
Use Elasticsearch for Search Projections

ADR-0008
GenAI Provider Abstraction

ADR-0009
Cloud Provider Strategy
```

## 62. Final Decision

Yunu.Commerce adopts Apache Kafka as its primary asynchronous event-streaming backbone.

Kafka will connect independently owned capabilities through explicit, versioned Integration Events while preserving Bounded Context autonomy.

The defining principle is:

> Business state is committed locally, Integration Events are published reliably, and independent consumers react asynchronously without reaching into the producer's implementation.
