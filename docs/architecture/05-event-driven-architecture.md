# Yunu.Commerce - Event-Driven Architecture

## 1. Purpose

This document defines the Event-Driven Architecture rules for Yunu.Commerce.

The objective is to enable loose coupling, asynchronous integration, scalability and independent evolution between Bounded Contexts.

Kafka is the initial event streaming platform.

The architecture must not depend on Kafka-specific concepts outside Infrastructure.

Events are architectural contracts.

Kafka is only the initial transport mechanism.

---

# 2. Core Principle

The fundamental rule is:

> Events describe facts that already happened.

Prefer:

```text
ProductCreated
PriceChanged
AvailabilityChanged
```

Avoid event names representing commands:

```text
CreateProduct
ChangePrice
UpdateAvailability
```

Commands express intent.

Events express facts.

---

# 3. Event Categories

Yunu.Commerce distinguishes between:

```text
Domain Events

Integration Events
```

These concepts have different purposes and lifecycles.

They must not be treated as the same thing.

---

# 4. Domain Events

Domain Events represent meaningful business facts inside a Bounded Context.

Examples:

```text
ProductCreatedDomainEvent

SkuActivatedDomainEvent

OfferActivatedDomainEvent

PriceChangedDomainEvent
```

Domain Events:

* belong to the Domain
* are raised by Aggregate behavior
* are transport-independent
* do not know Kafka
* do not know topics
* do not know serialization formats
* do not expose infrastructure-specific metadata

---

# 5. Integration Events

Integration Events communicate meaningful changes outside the owning Bounded Context.

Examples:

```text
ProductCreatedIntegrationEvent

ProductUpdatedIntegrationEvent

PriceChangedIntegrationEvent

AvailabilityChangedIntegrationEvent
```

Integration Events:

* are external contracts
* may be consumed by other contexts
* must be versioned
* must support backward compatibility
* must not expose internal Domain structure unnecessarily

---

# 6. Domain Event vs Integration Event

A Domain Event is not automatically an Integration Event.

Example:

```text
Product Aggregate
      │
      ▼
ProductCreatedDomainEvent
      │
      ▼
Application Handler
      │
      ▼
ProductCreatedIntegrationEvent
      │
      ▼
Outbox
      │
      ▼
Kafka
```

The translation boundary protects Domain evolution.

---

# 7. Event Flow

Typical event publication flow:

```text
Business Operation
      │
      ▼
Aggregate
      │
      ▼
Domain Event
      │
      ▼
Application
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

Kafka publication must not happen directly inside Domain behavior.

---

# 8. Initial Integration Events

Potential initial events include:

```text
Catalog

ProductCreated
ProductUpdated
ProductActivated
ProductDeactivated

SkuCreated
SkuUpdated
SkuActivated
SkuDeactivated


Sellers

SellerCreated
SellerActivated
SellerSuspended


Offers

OfferCreated
OfferActivated
OfferChanged
OfferDeactivated


Pricing

PriceCreated
PriceChanged
RegionalPriceChanged
PaymentPriceChanged
PriceExpired


Availability

AvailabilityChanged
RegionalAvailabilityChanged
BranchAvailabilityChanged


Fulfillment

FulfillmentNodeCreated
FulfillmentNodeChanged
FulfillmentNodeDeactivated


Search

ProductIndexed
ProductRemovedFromIndex


AI

ProductEnrichmentRequested
ProductEnrichmentCompleted
ProductEnrichmentFailed
```

Exact events must be introduced only when real use cases require them.

---

# 9. Event Envelope

Integration Events should use a standard envelope.

Minimum metadata:

```text
EventId
EventType
EventVersion
AggregateId
AggregateType
CorrelationId
CausationId
OccurredAtUtc
Source
Data
```

Conceptual example:

```json
{
  "eventId": "2f0d8f56-8708-4b64-92e5-d397117de083",
  "eventType": "ProductCreated",
  "eventVersion": 1,
  "aggregateId": "product-123",
  "aggregateType": "Product",
  "correlationId": "8bb66dcf-28c2-4b88-a403-5d290550ee7f",
  "causationId": "79d4fd28-5f18-4f0b-a33c-c5bb27759530",
  "occurredAtUtc": "2026-08-11T15:00:00Z",
  "source": "Yunu.Commerce.Catalog",
  "data": {}
}
```

---

# 10. EventId

Every published Integration Event must have a globally unique EventId.

EventId supports:

* deduplication
* tracing
* Inbox processing
* debugging
* auditability

EventId must remain stable across delivery retries.

A retry must not create a new logical event identity.

---

# 11. CorrelationId

CorrelationId links operations belonging to the same broader business flow.

Example:

```text
Create Product Request
        │
        ▼
ProductCreated
        │
        ▼
AI Enrichment
        │
        ▼
ProductEnriched
        │
        ▼
Search Indexed
```

All related operations should preserve the same CorrelationId where applicable.

---

# 12. CausationId

CausationId identifies the immediate event, command or operation that caused the current event.

Example:

```text
ProductCreated
EventId = A

        │
        ▼

ProductEnrichmentRequested
EventId = B
CausationId = A

        │
        ▼

ProductEnrichmentCompleted
EventId = C
CausationId = B
```

CorrelationId connects the full flow.

CausationId connects the immediate causal chain.

---

# 13. Event Timestamp

Integration Event timestamps must use UTC.

Prefer:

```text
DateTimeOffset
```

Do not publish ambiguous local times.

---

# 14. Event Version

Every Integration Event contract must be versionable.

Example:

```text
ProductCreated v1
ProductCreated v2
```

Breaking changes must never silently replace an existing contract.

---

# 15. Backward Compatibility

Existing consumers may still depend on older event contracts.

Changes should be evaluated as:

```text
Additive
Compatible

Breaking
Requires new event version
```

Examples of generally compatible changes:

```text
Adding optional fields
Adding metadata
```

Potential breaking changes:

```text
Removing fields
Renaming fields
Changing semantic meaning
Changing data type
Changing required behavior
```

---

# 16. Event Contract Ownership

The producer owns the event contract.

Consumers must not force the producer to expose internal Domain implementation details.

Event contracts should contain only what downstream consumers genuinely need.

---

# 17. Event Schema

Event schema must be explicit.

Possible serialization format:

```text
JSON
```

may initially be used.

Future schema registry capability may be introduced if operationally justified.

Potential future options include:

```text
JSON Schema
Avro
Protobuf
```

The architecture must not tie Domain code to a serialization technology.

---

# 18. Topic Strategy

Kafka topics should represent meaningful event streams.

Topic naming must be standardized.

Potential convention:

```text
yunu.commerce.catalog.events.v1
yunu.commerce.pricing.events.v1
yunu.commerce.availability.events.v1
```

or more granular streams if operational requirements justify them.

The exact convention must be documented in an ADR before production usage.

---

# 19. Topic Ownership

A Bounded Context owns the topics it produces.

Consumers subscribe to another context's published integration stream.

Consumers must not write arbitrary events into another context's owned topic.

---

# 20. Partition Key

Partition keys must be chosen according to ordering requirements.

Example:

```text
Product events
PartitionKey = ProductId
```

This can preserve order for events related to the same Product.

Possible keys include:

```text
ProductId
SkuId
OfferId
SellerId
```

depending on the event stream.

---

# 21. Ordering Guarantees

Do not assume global event ordering.

Kafka ordering is typically meaningful only within a partition.

If business logic requires ordering, the partitioning strategy must reflect that requirement.

Example:

```text
PriceChanged #1
PriceChanged #2
```

for the same Offer should use a consistent partition key when order matters.

---

# 22. Event Ordering and Consumers

Consumers must still tolerate delayed or duplicated events.

Where appropriate, use:

```text
Version
Sequence
OccurredAtUtc
AggregateVersion
```

to detect stale updates.

Do not rely solely on arrival order.

---

# 23. Transactional Outbox

Yunu.Commerce must avoid unsafe dual writes.

The problem:

```text
Save Product
      │
      ▼
Database committed

Publish event
      │
      ▼
Application crashes
```

Result:

```text
Database changed
Event never published
```

This is unacceptable for critical integration flows.

---

# 24. Outbox Pattern

The preferred flow is:

```text
Database Transaction
      │
      ├── Aggregate changes
      │
      └── Outbox Message
      │
      ▼
Commit
      │
      ▼
Outbox Processor
      │
      ▼
Kafka
```

Aggregate changes and Outbox Message must be stored atomically where the persistence technology supports transactional semantics.

---

# 25. Outbox Message

Potential Outbox fields:

```text
OutboxMessageId
EventId
EventType
EventVersion
Payload
OccurredAtUtc
CreatedAtUtc
PublishedAtUtc
RetryCount
Status
CorrelationId
CausationId
```

Implementation details belong to Infrastructure.

---

# 26. Outbox Processor

The Outbox Processor is responsible for:

```text
Reading pending Outbox messages
Publishing to Kafka
Handling transient failures
Updating publication status
Recording attempts
Emitting telemetry
```

It must not contain Domain business logic.

---

# 27. Outbox Idempotency

Publishing retries may occur.

The same logical EventId must be preserved.

Consumers must still be idempotent because the producer cannot guarantee exactly-once business processing across the entire distributed system.

---

# 28. Delivery Semantics

The architecture assumes:

```text
At-least-once delivery
```

as the safe baseline.

This means:

```text
Messages may be delivered more than once.
```

Consumers must be designed accordingly.

---

# 29. Consumer Idempotency

Every consumer handling state changes must tolerate duplicate events.

Example:

```text
Event A arrives
        │
        ▼
Processed

Event A arrives again
        │
        ▼
Must not duplicate business effect
```

Possible techniques include:

```text
Inbox Pattern
Processed EventId tracking
Database unique constraints
Idempotent upserts
Aggregate version checks
Deterministic operations
```

---

# 30. Inbox Pattern

An Inbox may persist processed EventIds.

Conceptual flow:

```text
Kafka Message
      │
      ▼
Check Inbox
      │
      ├── Already processed
      │       ▼
      │      Ignore safely
      │
      └── New
              │
              ▼
        Process use case
              │
              ▼
        Store Inbox record
```

Where possible, business changes and Inbox record should be committed atomically.

---

# 31. Consumer Transaction Boundary

A consumer should generally execute:

```text
Deserialize
Validate
Idempotency check
Invoke Application use case
Persist result
Record Inbox
Commit
Acknowledge
```

Acknowledgement must not occur before durable processing is complete.

---

# 32. Retry Strategy

Retries are appropriate for transient technical failures.

Examples:

```text
Temporary network failure
Database transient error
External provider timeout
Kafka temporary issue
```

Retries are not appropriate for permanent business failures.

---

# 33. Retry Policy

Retry strategy should consider:

```text
Maximum attempts
Backoff
Jitter
Operation idempotency
Dependency type
Failure classification
```

Avoid immediate infinite retries.

---

# 34. Business Failure vs Technical Failure

Example business failure:

```text
Referenced Seller does not exist
```

Example technical failure:

```text
Database temporarily unavailable
```

These may require different handling strategies.

A business-invalid message should not be retried forever.

---

# 35. Dead Letter Queue

Messages that cannot be successfully processed after configured retry policy may be moved to a Dead Letter Queue.

Conceptual flow:

```text
Kafka
  │
  ▼
Consumer
  │
  ├── Success
  │
  └── Failure
        │
        ▼
      Retry
        │
        ▼
      Retry exhausted
        │
        ▼
       DLQ
```

---

# 36. DLQ Metadata

DLQ records should preserve:

```text
Original Event
EventId
Original Topic
Consumer
Failure reason
Failure timestamp
Retry count
CorrelationId
CausationId
```

This enables diagnosis and controlled replay.

---

# 37. DLQ Is Not a Trash Bin

Messages in DLQ require operational visibility.

The platform should support:

```text
Alerting
Inspection
Metrics
Replay procedures
Root cause analysis
```

Messages must not disappear silently into DLQ forever.

---

# 38. Poison Messages

A poison message is one that consistently fails processing due to content or logic.

Examples:

```text
Invalid schema
Missing required semantic data
Unsupported event version
Corrupted payload
```

Poison messages should be isolated quickly rather than consuming retry capacity indefinitely.

---

# 39. Consumer Groups

Each independent reaction should normally use its own consumer group.

Example:

```text
ProductUpdated
        │
        ├── Search Indexer Group
        ├── AI Enrichment Group
        └── Analytics Group
```

Each group receives the event independently.

---

# 40. Consumer Independence

A failing Search consumer must not prevent AI or Analytics from consuming the same Catalog event.

Consumer groups provide operational isolation.

---

# 41. Event Choreography

EDA may use choreography when multiple independent contexts react to events.

Example:

```text
Catalog
   │
   │ ProductCreated
   ▼
Kafka
   │
   ├── AI reacts
   └── Search reacts
```

The producer does not coordinate those consumers directly.

---

# 42. Orchestration

Some long-running workflows may require explicit orchestration.

Example future workflow:

```text
Import Product
      │
      ▼
Validate
      │
      ▼
Enrich with AI
      │
      ▼
Approve
      │
      ▼
Publish
      │
      ▼
Index
```

If workflow complexity grows, an orchestrator or Saga pattern may be introduced.

Do not introduce Saga infrastructure before a real workflow requires it.

---

# 43. Eventual Consistency

Cross-context state is generally eventually consistent.

Example:

```text
Catalog Product updated
        │
        ▼
ProductUpdated published
        │
        ▼
Search updated milliseconds later
```

During that short period:

```text
Catalog = new state
Search = previous state
```

This is an expected distributed-system behavior.

---

# 44. Read-Your-Writes

If a use case requires immediate consistency after a write, it should read from the authoritative context rather than assuming every projection has already updated.

Example:

```text
Create Product
      │
      ▼
Return Product from Catalog
```

Do not immediately depend on Elasticsearch indexing completion unless the business requirement explicitly requires it.

---

# 45. Search Projection Events

Search may consume:

```text
ProductCreated
ProductUpdated
SkuUpdated
OfferChanged
PriceChanged
AvailabilityChanged
SellerChanged
```

Search builds its own denormalized read model.

It does not take ownership of source data.

---

# 46. AI Event Flow

Initial AI enrichment may follow:

```text
ProductCreated
      │
      ▼
ProductEnrichmentRequested
      │
      ▼
AI Worker
      │
      ▼
Generative AI Provider
      │
      ▼
ProductEnrichmentCompleted
      │
      ▼
Catalog Application
```

AI-generated changes still require Catalog ownership and validation.

---

# 47. Event Loops

Consumers must avoid accidental event loops.

Example problem:

```text
ProductUpdated
      │
      ▼
AI enrichment
      │
      ▼
ProductUpdated
      │
      ▼
AI enrichment again
```

Potential protections include:

```text
Event origin metadata
Change reason
Processing flags
Causation chain
Explicit enrichment state
```

Event cycles must be deliberately designed.

---

# 48. Integration Event Granularity

Events should represent meaningful facts.

Avoid events that are too technical:

```text
DatabaseRowUpdated
MongoDocumentSaved
CacheKeyChanged
```

Prefer business language:

```text
ProductUpdated
PriceChanged
SellerActivated
```

---

# 49. Avoid Event Explosion

Do not publish an Integration Event for every internal property mutation.

Events should exist when:

```text
another context has a meaningful reason to react
```

Internal details may remain Domain Events or ordinary state changes.

---

# 50. Event Payload Size

Integration Events should remain reasonably small.

Large binary assets should not be embedded in Kafka messages.

Use object storage and publish references when necessary.

Example:

```text
ProductImportFileUploaded
ObjectReference = ...
```

---

# 51. Event Snapshots

Avoid publishing complete Aggregate snapshots by default.

Prefer the minimum information required by consumers.

A consumer that needs more information may:

```text
build its projection from events
or
query an explicit Application API
```

The choice depends on coupling and consistency requirements.

---

# 52. Integration Event Immutability

Published Integration Events are immutable facts.

Do not alter previously published historical events.

Corrections should be expressed as new events.

---

# 53. Event Naming Convention

Event names should use past tense.

Examples:

```text
ProductCreated
ProductUpdated
PriceChanged
AvailabilityChanged
SellerActivated
```

Avoid vague names:

```text
ProductEvent
DataChanged
EntityUpdated
```

Names should communicate business meaning.

---

# 54. Consumer Naming

Consumers should express what reaction they perform.

Examples:

```text
ProductCreatedSearchProjectionConsumer

PriceChangedSearchProjectionConsumer

ProductCreatedAIEnrichmentConsumer
```

Avoid generic:

```text
EventConsumer
KafkaHandler
MessageProcessor
```

when the business reaction is known.

---

# 55. Event Handler Responsibility

A message consumer is an inbound adapter.

Its responsibility is to:

```text
receive
deserialize
validate envelope
handle idempotency
translate to Application input
invoke Application
manage acknowledgement
```

Business rules remain in Domain/Application.

---

# 56. Serialization

Serialization belongs to messaging Infrastructure.

Application should operate on typed contracts.

Provider-specific serializer configuration must not leak into Domain.

---

# 57. Observability

Every producer and consumer should emit telemetry.

Producer telemetry may include:

```text
EventType
EventId
Topic
PartitionKey
Latency
Success / Failure
Retry count
```

Consumer telemetry may include:

```text
EventType
EventId
Consumer
Lag
Processing duration
Retry count
Success / Failure
DLQ status
```

---

# 58. Distributed Tracing

Event metadata should propagate distributed tracing context when possible.

Flow:

```text
HTTP Request
TraceId A
      │
      ▼
ProductCreated
TraceId A
      │
      ▼
AI Consumer
TraceId A
```

Exact OpenTelemetry propagation mechanics belong to Infrastructure.

---

# 59. Logging

Structured logging should include:

```text
EventId
EventType
CorrelationId
CausationId
AggregateId
Consumer
```

Do not log sensitive payloads indiscriminately.

---

# 60. Security

Integration messages must not contain secrets.

Do not publish:

```text
API keys
Passwords
Access tokens
Private keys
Unnecessary personal information
```

Only required business information should be included.

---

# 61. Authorization and Events

An Integration Event communicates a fact.

Consumers must still enforce their own business invariants.

Receiving an event from a trusted source does not mean a consumer must bypass its own Domain rules.

---

# 62. Schema Validation

Consumers should reject unsupported or malformed event schemas predictably.

Validation should distinguish:

```text
Unsupported event version
Malformed envelope
Invalid payload
Business-invalid message
Transient infrastructure failure
```

These categories influence retry and DLQ behavior.

---

# 63. Idempotency Scope

Idempotency is defined by the effect of the consumer.

Example:

```text
Search indexing
```

can often use deterministic upsert by ProductId.

Example:

```text
Increment balance
```

would require stronger duplicate protection.

Each consumer must define its idempotency strategy explicitly.

---

# 64. Projection Rebuild

Read projections should be designed so they can be rebuilt where practical.

For Search:

```text
Authoritative data
      │
      ▼
Projection process
      │
      ▼
Elasticsearch
```

Elasticsearch should not become irreplaceable authoritative state.

---

# 65. Replay

Event replay may be required for:

```text
Projection rebuild
Consumer recovery
Bug correction
New consumer initialization
```

Consumers must be designed with replay behavior in mind.

Replay must not accidentally trigger inappropriate external side effects.

---

# 66. Replay-Safe Consumers

Consumers that call external systems require special care during replay.

Example:

```text
ProductCreated
      │
      ▼
Send external marketplace publication
```

Replaying historical events could duplicate external actions.

Such consumers require explicit replay safeguards.

---

# 67. Event Retention

Kafka retention policy is an operational concern.

Retention must be selected based on:

```text
Replay requirements
Storage cost
Compliance
Recovery strategy
Projection rebuild needs
```

Retention strategy should be documented before production deployment.

---

# 68. Compacted Topics

Compacted topics may be useful for state-oriented streams where latest value by key matters.

They should be introduced only when semantics justify them.

Do not use topic compaction as a substitute for proper persistence design.

---

# 69. Event Contract Location

Integration Event contracts should live in explicit Contracts projects.

Example:

```text
Yunu.Commerce.Catalog.Contracts
```

Possible structure:

```text
Events/
  ProductCreatedIntegrationEvent.cs
  ProductUpdatedIntegrationEvent.cs
```

Domain Events remain inside Domain.

---

# 70. No Foreign Domain Dependency

A consumer must not reference another context's Domain project to deserialize events.

Forbidden:

```text
Search
    → Catalog.Domain.Product
```

Preferred:

```text
Search
    → Catalog.Contracts.ProductUpdatedIntegrationEvent
```

This preserves context independence.

---

# 71. Event Metadata Standardization

Common event envelope metadata may use a small shared technical contract where justified.

The shared abstraction must remain technical and stable.

Do not place business event payloads into Shared Kernel.

---

# 72. Producer Responsibility

A producer must ensure:

```text
valid event contract
stable EventId
correct event version
correct partition key
correlation metadata
reliable Outbox persistence
```

---

# 73. Consumer Responsibility

A consumer must ensure:

```text
supported event version
idempotency
correct failure classification
retry behavior
DLQ behavior
observability
safe acknowledgement
```

---

# 74. Kafka Abstraction

Application and Domain must not reference Kafka libraries.

Kafka-specific implementation belongs to:

```text
EventBus Infrastructure
Worker Infrastructure
Host configuration
```

The core should depend on abstractions such as:

```text
IIntegrationEventPublisher
```

---

# 75. Local Development

Local development may use Kafka in Docker.

The event architecture should behave similarly across:

```text
Local
Integration Test
Staging
Production
```

Environment differences must be configuration concerns, not Domain concerns.

---

# 76. Integration Testing

Critical event flows require integration tests.

Examples:

```text
Outbox message is published

Consumer processes message

Duplicate event is safe

Unsupported event goes to expected failure path

Search projection updates

AI enrichment event flow completes
```

Kafka Testcontainers may be used where appropriate.

---

# 77. Contract Testing

Event contracts should have compatibility tests where practical.

Tests may verify:

```text
required metadata
serialization
deserialization
version compatibility
optional field behavior
```

---

# 78. Failure Scenario Testing

Tests should include:

```text
consumer crash after processing
consumer crash before commit
Kafka unavailable
database unavailable
duplicate event
out-of-order event
malformed event
DLQ path
```

Distributed-system behavior must be tested deliberately.

---

# 79. Initial EDA Vertical Slice

The first event-driven vertical slice should be:

```text
Catalog API
      │
      ▼
Create Product
      │
      ▼
Catalog Database
      +
Outbox
      │
      ▼
Outbox Processor
      │
      ▼
Kafka
      │
      ├────────► AI Enrichment Consumer
      │
      └────────► Search Projection Consumer
```

This validates the core EDA architecture early.

---

# 80. No Premature Event Sourcing

Yunu.Commerce uses Event-Driven Architecture.

This does not mean the platform automatically uses Event Sourcing.

Aggregate state may be persisted normally.

Events are used for:

```text
business signaling
integration
projections
asynchronous processing
```

Event Sourcing may be evaluated separately if a future domain genuinely benefits from it.

---

# 81. Eventual Consistency Principle

The platform must embrace eventual consistency where it reduces coupling without violating business correctness.

Do not force distributed transactions simply to preserve the illusion of one database.

---

# 82. Architecture Decision Checklist

Before creating a new Integration Event, answer:

```text
What business fact occurred?

Who owns the fact?

Does another context genuinely need to react?

Is synchronous communication more appropriate?

What is the event identity?

What is the partition key?

What ordering matters?

What is the compatibility strategy?

How is it published reliably?

How does the consumer remain idempotent?

What happens when processing fails?

Can the event be safely replayed?
```

If these questions do not have reasonable answers, the event is not ready.

---

# 83. Core Rule

The central EDA rule of Yunu.Commerce is:

> Events reduce coupling only when contracts, ownership, reliability and idempotency are explicit.

Kafka alone does not create Event-Driven Architecture.

The architecture comes from how business facts are modeled and exchanged.

---

# 84. Final Principle

Bounded Contexts own their state.

Events communicate facts.

Outbox provides reliable publication.

Inbox and idempotency provide safe consumption.

Kafka transports the events.

The Domain remains independent from the transport.
