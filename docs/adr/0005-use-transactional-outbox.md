# ADR-0005: Use Transactional Outbox

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Reliable publication of Integration Events

## 1. Context

Yunu.Commerce uses Event-Driven Architecture and Kafka to propagate business changes between independently owned capabilities.

A typical operation may need to:

```text
1. Persist business state
2. Publish an Integration Event
```

Example:

```text
Create Product
    │
    ├── Save Product in Catalog database
    └── Publish ProductCreated to Kafka
```

These operations involve different infrastructure systems and cannot safely be treated as one ordinary local database transaction.

If the database commit succeeds but Kafka publication fails, downstream consumers may never learn about the committed business change.

If Kafka publication succeeds but the database transaction later fails, consumers may receive an event describing state that never became canonical.

This is the distributed dual-write problem.

## 2. Decision

Yunu.Commerce will use the Transactional Outbox pattern for reliable publication of Integration Events.

The business state change and its Outbox message are persisted atomically in the same local transaction whenever the underlying persistence technology supports the required transactional boundary.

Conceptually:

```text
Application Use Case
        │
        ▼
Domain Operation
        │
        ▼
Local Database Transaction
        │
        ├── Persist Aggregate
        │
        └── Persist Outbox Message
                │
                ▼
             COMMIT
```

A separate publisher later delivers the Outbox message to Kafka:

```text
Outbox
   │
   ▼
Outbox Publisher
   │
   ▼
Kafka
```

## 3. Core Guarantee

The Outbox provides this important guarantee:

> If the business transaction commits, the intent to publish the corresponding Integration Event is durably recorded.

It does not mean that Kafka receives the event exactly once.

Kafka publication and consumer processing remain compatible with at-least-once delivery.

## 4. Why Not Direct Publication

The following pattern is forbidden for reliable business events:

```text
await repository.SaveAsync();

await kafka.PublishAsync();
```

A process crash between these operations creates inconsistent distributed state.

The reverse order is also unsafe:

```text
await kafka.PublishAsync();

await repository.SaveAsync();
```

because consumers may observe an event for a transaction that ultimately fails.

## 5. Transaction Boundary

The Outbox message must be written inside the same persistence transaction as the canonical business state.

Example:

```text
Catalog Transaction
│
├── Product
└── OutboxMessage
```

For relational persistence:

```text
BEGIN TRANSACTION

UPDATE / INSERT business data

INSERT OutboxMessage

COMMIT
```

For MongoDB, transaction strategy must respect the chosen document/session architecture and deployment capabilities.

## 6. Outbox Ownership

Each producing Bounded Context owns its own Outbox.

Conceptually:

```text
Catalog
├── Catalog Data
└── Catalog Outbox

Pricing
├── Pricing Data
└── Pricing Outbox

Availability
├── Availability Data
└── Availability Outbox
```

There is no global business Outbox database shared by every context.

## 7. Outbox Message

A conceptual Outbox record contains:

```text
OutboxMessage

Id
EventId
EventType
EventVersion
Payload
OccurredAtUtc
CorrelationId
CausationId
CreatedAtUtc
ProcessedAtUtc
RetryCount
LastError
```

Exact physical representation may vary by persistence technology.

## 8. Event Payload

The Outbox stores the serialized Integration Event or sufficient immutable information to publish it reliably.

The payload must represent an Integration Event contract, not an arbitrary Domain Entity serialization.

## 9. Domain Events and Outbox

Domain Events are not automatically Kafka messages.

Conceptually:

```text
Domain Operation
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
```

Only events intentionally exposed outside the context become Integration Events.

## 10. Outbox Publisher

A background process publishes pending Outbox messages.

Responsibilities:

```text
read pending messages
publish to Kafka
record successful publication
retry transient failures
emit telemetry
respect cancellation
avoid unbounded concurrency
```

It contains no business rules.

## 11. Publisher Deployment

The publisher may initially run as:

```text
BackgroundService
```

inside an appropriate Host or as a dedicated Worker.

The architecture must allow later independent deployment.

## 12. Publication Semantics

The expected publication semantics are:

```text
at least once
```

A message may be published more than once if a failure occurs after Kafka accepts the message but before the Outbox record is marked as processed.

Therefore consumers must be idempotent.

## 13. Example Duplicate Scenario

```text
Outbox Publisher
      │
      ▼
Kafka accepts Event A
      │
      ▼
Process crashes
      │
      X
Outbox not marked processed
      │
      ▼
Process restarts
      │
      ▼
Event A published again
```

This behavior is acceptable.

Consumer idempotency handles the duplicate.

## 14. EventId Stability

Retries of the same Outbox message must preserve the same:

```text
EventId
```

A retry must not generate a new logical event identity.

This enables downstream deduplication.

## 15. Processed State

After successful publication, the Outbox message is marked as processed.

Conceptually:

```text
ProcessedAtUtc = timestamp
```

The implementation must account for the crash window between broker acknowledgment and local status update.

## 16. Retry

Transient Kafka publication failures may be retried.

Examples:

```text
network interruption
temporary broker unavailability
temporary timeout
```

Retries must be:

```text
bounded
observable
backed off
cancellation-aware
```

## 17. Permanent Failures

Permanent serialization or contract failures must not retry forever.

Examples:

```text
invalid event payload
unsupported serialization
corrupted Outbox record
```

They require operational visibility and remediation.

## 18. Outbox Failure State

Implementations may record:

```text
RetryCount
LastAttemptAtUtc
LastError
FailureStatus
```

where useful.

The exact model is an Infrastructure concern.

## 19. Outbox Backlog

Outbox backlog is a critical operational metric.

Important measurements include:

```text
pending message count
oldest pending message age
publication rate
failure rate
retry count
```

A growing backlog indicates integration degradation even when the business API remains healthy.

## 20. Ordering

Where downstream ordering matters, Outbox publication must preserve the relevant entity sequence as far as the architecture requires.

Kafka partition keys remain responsible for broker-side ordering boundaries.

Potential key examples:

```text
ProductId
OfferId
SkuId + FulfillmentNodeId
```

## 21. Concurrency

Multiple publisher instances may process Outbox records concurrently.

The implementation must prevent uncontrolled duplicate work through appropriate claiming/locking strategies.

Duplicates may still occur and must remain safe.

## 22. Batch Publishing

The Outbox Publisher may read messages in bounded batches.

Example:

```text
Read 100 pending messages
        │
        ▼
Publish with bounded concurrency
```

Batch size must be configurable and performance-tested.

## 23. Polling

The initial Outbox Publisher may poll for pending messages.

Conceptually:

```text
Poll
  │
  ▼
Pending messages?
  │
  ├── No → wait
  └── Yes → publish
```

Polling frequency should balance latency and database load.

## 24. Alternative Outbox Dispatch

Future implementations may evaluate:

```text
database notifications
CDC-based Outbox relay
Debezium
managed change streams
```

without changing Domain semantics.

Such changes require architectural evaluation.

## 25. Outbox Retention

Successfully processed messages should not remain forever in the operational Outbox.

A retention/cleanup policy must be defined.

Possible strategy:

```text
Processed
   │
   ▼
retain for operational window
   │
   ▼
archive or delete
```

Retention depends on audit and troubleshooting requirements.

## 26. Outbox Is Not the Audit Log

The Outbox exists to guarantee message publication.

It should not automatically be treated as the permanent business audit store.

If permanent auditing is required, implement it explicitly.

## 27. Inbox Relationship

Outbox solves reliable producer publication.

Inbox solves consumer duplicate processing.

Conceptually:

```text
Producer
  │
  ▼
Outbox
  │
  ▼
Kafka
  │
  ▼
Inbox
  │
  ▼
Consumer
```

The patterns are complementary.

## 28. Inbox Decision

Consumers requiring durable deduplication should implement Inbox or equivalent idempotency mechanisms.

Inbox persistence belongs to the consuming capability.

## 29. Search Projection Example

```text
Catalog
   │
   ▼
ProductCreated
   │
   ▼
Catalog Outbox
   │
   ▼
Kafka
   │
   ▼
Search Consumer
   │
   ▼
Inbox / Idempotency
   │
   ▼
Elasticsearch
```

## 30. Pricing Example

```text
Change Price
    │
    ▼
Pricing Domain
    │
    ▼
Pricing Transaction
    │
    ├── Save Price
    └── Save PriceChanged Outbox
             │
             ▼
           COMMIT
             │
             ▼
      Outbox Publisher
             │
             ▼
           Kafka
```

## 31. Availability Example

Availability may produce high event volume.

Conceptually:

```text
Update Availability
        │
        ▼
Availability persistence
        +
Outbox message
        │
        ▼
Kafka
        │
        ├── Redis Projection
        └── Search Projection
```

The Outbox implementation must be load-tested for this workload.

## 32. MongoDB Considerations

For MongoDB-backed contexts, Outbox atomicity must be designed deliberately.

Potential approaches depend on Aggregate storage design and MongoDB transaction capabilities.

The implementation must not claim transactional Outbox guarantees unless the business state and Outbox write are actually atomic under the selected approach.

## 33. Relational Considerations

For relational contexts, business changes and Outbox inserts should share the same database transaction.

Example:

```text
DbContext / transaction
│
├── Price changes
└── Outbox rows
```

Commit once.

## 34. Persistence Abstraction

Application code should not contain Kafka publication logic.

A Unit of Work or persistence pipeline may coordinate:

```text
Aggregate persistence
Domain Event collection
Integration Event creation
Outbox persistence
```

The exact mechanism should remain simple and explicit.

## 35. No Hidden Magic

The Outbox implementation should be understandable and testable.

Avoid excessive framework magic that makes it unclear:

```text
when events are collected
when they are serialized
when Outbox rows are inserted
when the transaction commits
```

Correctness is more important than cleverness.

## 36. Serialization Timing

Integration Event serialization may occur before Outbox persistence.

The resulting payload should be immutable for that Outbox record.

Changes to event classes after persistence must not alter historical pending messages.

## 37. Schema Version

Every Outbox Integration Event must carry its contract version.

Example:

```text
EventType = ProductCreated
EventVersion = 1
```

This supports independent consumer evolution.

## 38. Correlation Metadata

Outbox records should preserve:

```text
CorrelationId
CausationId
OccurredAtUtc
Producer
```

so publication does not lose distributed tracing context.

## 39. UTC

All Outbox timestamps use UTC.

Examples:

```text
OccurredAtUtc
CreatedAtUtc
ProcessedAtUtc
LastAttemptAtUtc
```

## 40. Observability

OpenTelemetry and structured logs should cover:

```text
Outbox creation
Outbox polling
Kafka publication
publication latency
retry
failure
backlog
```

A trace should connect the original operation to publication where practical.

## 41. Metrics

Recommended metrics:

```text
outbox_pending_total
outbox_oldest_pending_seconds
outbox_published_total
outbox_publish_failed_total
outbox_retry_total
outbox_publish_duration
```

Exact metric naming may follow platform conventions.

## 42. Health and Readiness

A temporary Kafka outage does not necessarily mean the business API must reject all writes if the local Outbox remains healthy.

This is a major benefit of the pattern.

Conceptually:

```text
Kafka unavailable
       │
       ▼
Business transaction still commits
       │
       ▼
Outbox accumulates
       │
       ▼
Kafka recovers
       │
       ▼
Publisher drains backlog
```

Operational limits must prevent unlimited backlog growth.

## 43. Backlog Protection

The platform should define operational thresholds for:

```text
maximum acceptable backlog
maximum event age
database storage consumption
alerting
```

If limits become dangerous, protective operational policies may be required.

## 44. Security

Outbox payloads may contain business data.

Therefore:

```text
do not store secrets
minimize sensitive data
protect database access
apply retention
avoid unrestricted payload logging
```

## 45. Testing

Required test categories include:

```text
business state and Outbox commit together
transaction rollback removes both
publisher publishes pending message
successful publication marks processed
failed publication remains pending
retry preserves EventId
duplicate publication is safe downstream
batch concurrency is bounded
cancellation is respected
```

## 46. Integration Tests

Use real persistence infrastructure in integration tests where practical.

Examples:

```text
MongoDB via Testcontainers
relational database via Testcontainers
Kafka via Testcontainers
```

Critical transactional guarantees should not be tested only with mocks.

## 47. Failure Injection Tests

Important scenarios should deliberately simulate:

```text
database commit failure
Kafka unavailable
publisher crash
timeout after Kafka send
duplicate event delivery
process restart with pending messages
```

The system must recover without corrupting canonical state.

## 48. Architecture Tests

Architecture tests should enforce:

```text
Domain does not reference Outbox persistence.

Domain does not reference Kafka.

Application handlers do not instantiate Kafka producers.

Infrastructure owns Outbox storage and publication.

Integration Events remain explicit contracts.
```

## 49. Initial Implementation Strategy

Recommended initial sequence:

```text
1. Define Integration Event envelope

2. Define OutboxMessage infrastructure model

3. Persist Outbox with Catalog Product transaction

4. Implement Outbox query/claim logic

5. Implement Kafka publisher adapter

6. Implement Outbox Publisher Worker

7. Add retry and telemetry

8. Implement ProductCreated consumer

9. Add consumer idempotency

10. Test crash/duplicate scenarios
```

## 50. First Vertical Slice

The first complete Outbox flow should be:

```text
POST Product
     │
     ▼
Catalog Application
     │
     ▼
Catalog Domain
     │
     ▼
MongoDB Transaction
     │
     ├── Product
     └── ProductCreated Outbox
             │
             ▼
           COMMIT
             │
             ▼
      Outbox Publisher
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

## 51. Consequences

### Positive

```text
reliable event publication
no unsafe database/Kafka dual-write
producer resilience during broker outages
replayable pending publication
clear operational state
preservation of local transaction boundaries
```

### Negative

```text
additional persistence
background publisher
eventual publication delay
cleanup requirements
duplicate publication remains possible
additional monitoring
more integration tests
```

These tradeoffs are accepted.

## 52. Alternatives Considered

### Direct Database + Kafka Dual Write

Rejected because it cannot guarantee consistency across independent systems.

### Distributed Transaction Across Database and Kafka

Rejected as the default because it introduces complexity, coupling and infrastructure constraints inconsistent with the architecture.

### Publish Before Database Commit

Rejected because consumers could observe events for failed transactions.

### Publish After Database Commit Without Outbox

Rejected because process failure can permanently lose the event.

### CDC as the Only Integration Mechanism

Not selected as the initial pattern.

CDC may later relay Outbox records, but business Integration Events remain explicit contracts.

## 53. Copilot Rules

GitHub Copilot must:

```text
Use Outbox for reliable Integration Event publication.

Never publish Kafka directly from Domain.

Never publish Kafka as an unrelated second step after database commit.

Persist business state and Outbox atomically.

Preserve EventId across retries.

Assume duplicate publication can happen.

Make downstream consumers idempotent.

Use UTC timestamps.

Propagate CorrelationId and CausationId.

Use bounded polling and concurrency.

Use CancellationToken.

Add retry only for transient failures.

Expose Outbox backlog metrics.

Do not log entire sensitive payloads.

Do not use Outbox as a permanent audit log unless separately designed.

Do not create a global shared Outbox across Bounded Contexts.
```

## 54. Relationship to Other ADRs

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
```

It supports:

```text
ADR-0006
Use Redis for Distributed Cache

ADR-0007
Use Elasticsearch for Search Projections

ADR-0008
GenAI Provider Abstraction
```

because those asynchronous consumers require reliable upstream event publication.

## 55. Final Decision

Yunu.Commerce adopts the Transactional Outbox pattern for reliable publication of Integration Events.

The canonical business change and publication intent are committed together inside the producing Bounded Context.

Kafka delivery then occurs asynchronously through an Outbox Publisher.

The defining principle is:

> Commit business truth and the intent to communicate it atomically. Deliver asynchronously, expect duplicates, and make consumers idempotent.
