# ADR-0006: Use Redis for Distributed Cache

- **Status:** Accepted
- **Date:** 2026-08-11
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Distributed caching, low-latency projections and ephemeral distributed state

## 1. Context

Yunu.Commerce contains read-intensive and latency-sensitive commerce capabilities.

Examples include:

```text
Product lookup
Current price lookup
National availability
Regional availability
Fulfillment information
Freight quotation
Search-related supporting data
```

Some canonical databases may not be appropriate for every high-frequency read.

The platform therefore requires a distributed, low-latency data layer that can reduce pressure on canonical stores while remaining compatible with the DDD, Clean Architecture, Hexagonal Architecture and Event-Driven Architecture decisions.

## 2. Decision

Yunu.Commerce will use Redis as the primary distributed cache and low-latency projection technology.

Conceptually:

```text
Application
    │
    ▼
Cache Port
    │
    ▼
Redis Adapter
```

Redis is an Infrastructure concern.

Domain projects must not reference Redis libraries or Redis-specific data structures.

## 3. Redis Is Not the Default Source of Truth

The fundamental rule is:

> Redis is derived or ephemeral state unless a future ADR explicitly establishes a different ownership model.

Canonical commerce data remains owned by the appropriate Bounded Context.

Examples:

```text
Catalog canonical data
→ Catalog persistence

Pricing canonical data
→ Pricing persistence

Availability canonical data
→ Availability persistence

Redis
→ optimized derived representation
```

## 4. Primary Redis Use Cases

Redis may initially be used for:

```text
distributed cache
availability projections
frequently accessed reference data
short-lived freight quote cache
idempotency state where appropriate
rate-limiting state
distributed coordination where justified
```

Each use case must define its own consistency and expiration requirements.

## 5. Availability Projection

Availability is expected to be a high-frequency capability.

A primary Redis use case is a low-latency Availability projection.

Conceptually:

```text
Availability Domain
       │
       ▼
AvailabilityChanged
       │
       ▼
Outbox
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

Reads can then use the projection where its consistency guarantees are sufficient.

## 6. National Availability

A Redis projection may represent national availability.

Conceptual key:

```text
availability:national:{skuId}
```

Possible value:

```text
SellableQuantity
IsAvailable
Version
UpdatedAtUtc
```

Exact key and serialization formats belong to Infrastructure.

## 7. Regional Availability

Regional availability may use a key such as:

```text
availability:region:{regionId}:{skuId}
```

or another structure selected after access-pattern testing.

The Domain must not depend on the physical Redis key design.

## 8. Fulfillment-Node Availability

Where node-level reads are required:

```text
availability:node:{fulfillmentNodeId}:{skuId}
```

may be used conceptually.

Actual representation must consider memory use and access patterns.

## 9. Event-Driven Cache Updates

For projections derived from Domain changes, event-driven updates are preferred.

Example:

```text
PriceChanged
    │
    ▼
Kafka
    │
    ▼
Price Cache Projection
    │
    ▼
Redis
```

This avoids requiring the owning Domain transaction to synchronously update Redis.

## 10. Avoid Unsafe Dual Writes

Forbidden as a reliability strategy:

```text
Save canonical database
        │
        ▼
Update Redis directly
```

as two unrelated writes whose consistency is assumed.

For important projections, prefer:

```text
Canonical Commit
      │
      ▼
Outbox
      │
      ▼
Kafka
      │
      ▼
Redis Projection
```

## 11. Cache-Aside

For suitable query use cases, Cache-Aside may be used.

Conceptually:

```text
Query
  │
  ▼
Redis
  │
  ├── HIT → return
  │
  └── MISS
       │
       ▼
Canonical Read Store
       │
       ▼
Populate Redis
       │
       ▼
Return
```

Cache-Aside is appropriate only when stale-read semantics are understood.

## 12. Cache Invalidation

Cache invalidation must be explicit.

Possible strategies:

```text
event-driven invalidation
event-driven replacement
TTL expiration
versioned keys
cache-aside refresh
```

Do not rely on indefinite stale entries.

## 13. TTL

Every ordinary cache entry should have an intentional TTL unless there is a documented reason not to.

TTL must reflect business freshness requirements.

Examples may differ substantially:

```text
reference data
→ longer TTL

freight quotation
→ short TTL

high-frequency availability
→ event-updated projection with freshness/version controls
```

No universal TTL should be hardcoded across all capabilities.

## 14. Projection vs Cache

The architecture distinguishes:

```text
Cache
→ disposable acceleration of another read

Projection
→ intentionally maintained read model
```

Both may use Redis.

The distinction matters for rebuilding, TTL and consistency behavior.

## 15. Redis Failure

Core canonical commerce data must survive complete Redis loss.

Conceptually:

```text
Redis lost
    │
    ▼
Performance degrades / projections rebuild
    │
    ▼
Canonical business data remains intact
```

This is one reason Redis is not the default source of truth.

## 16. Graceful Degradation

Where appropriate, an unavailable Redis instance should result in fallback to a canonical/read store.

Example:

```text
Redis unavailable
       │
       ▼
Catalog query
       │
       ▼
Canonical Catalog store
```

Fallback behavior depends on latency and load constraints.

## 17. Avoid Cache Stampede

High-traffic keys can cause a cache stampede after expiration.

Potential mitigation:

```text
jittered TTL
single-flight locking
stale-while-revalidate where appropriate
background refresh
bounded concurrency
```

Implementation should be introduced where measurements justify it.

## 18. Key Naming

Redis keys should use consistent namespaces.

Conceptually:

```text
yunu:{context}:{purpose}:{identifier}
```

Examples:

```text
yunu:availability:national:{skuId}
yunu:availability:region:{regionId}:{skuId}
yunu:freight:quote:{hash}
```

Exact conventions should be centralized in Infrastructure helpers.

## 19. Key Versioning

When cache schema changes incompatibly, keys may include a schema version.

Example:

```text
yunu:v2:availability:region:{regionId}:{skuId}
```

This enables safe migrations without interpreting old values as new structures.

## 20. Serialization

Redis values must use explicit serialization contracts.

Avoid serializing Domain Aggregates directly.

Preferred:

```text
AvailabilityCacheEntry
ProductCacheEntry
FreightQuoteCacheEntry
```

These are Infrastructure/read-model contracts.

## 21. Domain Independence

Forbidden:

```text
Availability.Domain
    │
    ▼
StackExchange.Redis
```

Correct:

```text
Availability Application
       │
       ▼
IAvailabilityReadStore
       │
       ▼
RedisAvailabilityReadStore
```

## 22. Port Naming

Ports should describe capabilities rather than technology.

Avoid:

```text
IRedisService
IRedisRepository
```

Prefer:

```text
IAvailabilityReadStore
IDistributedCache
IFreightQuoteCache
IIdempotencyStore
```

when those abstractions are actually needed.

## 23. Generic Cache Abstraction

A small technical distributed-cache abstraction may be used for simple caching.

However, complex projections should use purpose-specific ports.

Do not hide important business read semantics behind an overly generic:

```text
Get<T>(string key)
Set<T>(string key)
```

throughout Application code.

## 24. Pricing Cache

Pricing may use Redis for frequently accessed current-price projections if measurements justify it.

Canonical Price remains in Pricing persistence.

Potential flow:

```text
PriceChanged
    │
    ▼
Kafka
    │
    ▼
Price Projection
    │
    ▼
Redis
```

## 25. Freight Cache

Freight quotation may be expensive because it can involve external providers.

A short-lived cache may be used based on a normalized quotation key.

Potential inputs:

```text
origin
destination
SKU dimensions/weight
quantity
carrier/service
commercial context
```

The cache key must include all fields that materially affect the quotation.

## 26. Freight Cache Safety

Never return a cached freight quote beyond the period where its price/SLA is considered valid.

Freight cache policy belongs to the Freight capability.

## 27. Catalog Cache

Catalog queries may use Redis only where it provides measurable value.

Elasticsearch is expected to handle broad search/discovery workloads.

Redis should not duplicate the entire Catalog without a concrete access-pattern reason.

## 28. Seller Cache

Seller status/reference data may be cached when repeatedly required by high-volume flows.

Authorization-sensitive or lifecycle-critical decisions must account for acceptable staleness.

## 29. Idempotency

Redis may support short-lived idempotency keys for selected synchronous operations.

However, durable event-consumer deduplication may require persistent Inbox storage.

Redis expiration must not accidentally remove deduplication guarantees earlier than required.

## 30. Rate Limiting

Redis may be used for distributed rate-limiting state.

Examples:

```text
public API limits
seller integration limits
AI request limits
external-provider protection
```

Rate limiting remains an Infrastructure/API concern.

## 31. Distributed Locks

Redis distributed locking may be used only when a real coordination requirement exists.

Do not introduce distributed locks as a substitute for proper Aggregate concurrency or event ordering.

## 32. Lock Safety

Any distributed lock implementation must define:

```text
expiration
ownership
failure recovery
maximum critical-section duration
```

Avoid indefinite locks.

## 33. Atomic Operations

Redis atomic operations may be used for technical counters or coordination.

Business invariants should not silently migrate into Redis scripts without an explicit architectural decision.

## 34. Memory

Redis is memory-oriented infrastructure.

Data design must account for:

```text
key cardinality
value size
TTL
eviction policy
replication overhead
regional availability volume
```

High-cardinality projections require capacity testing.

## 35. Eviction

Eviction policy must match the workload.

If an entry is required for correctness, it should not be modeled as an ordinary evictable cache entry.

Canonical correctness must not depend on accidental cache survival.

## 36. Persistence Configuration

Redis persistence mechanisms may be configured for operational resilience where useful.

This does not automatically make Redis the canonical Domain database.

## 37. High Availability

Production Redis should support appropriate high-availability capabilities based on the chosen deployment.

The cloud-specific implementation is addressed by ADR-0009.

## 38. Security

Production Redis must use:

```text
encrypted transport where supported
authentication/identity
network isolation
least privilege
secret management
```

Redis should not be publicly exposed.

## 39. Sensitive Data

Do not place unnecessary sensitive information into cache values.

Cached data must follow the same security classification as its source.

## 40. Observability

Redis telemetry should include:

```text
cache hit rate
cache miss rate
latency
errors
timeouts
memory utilization
evictions
connection usage
projection lag
```

## 41. Projection Lag

For event-driven Redis projections, measure the delay between:

```text
business event occurrence
```

and:

```text
Redis projection update
```

This determines actual data freshness.

## 42. Tracing

Redis operations should participate in OpenTelemetry distributed traces where supported.

Avoid logging complete cache payloads.

## 43. Timeout

Every Redis operation must have bounded timeout behavior.

A cache must not become a reason for indefinite request blocking.

## 44. Retry

Redis retries should be conservative.

Repeated retries during an outage can amplify load.

Retry policies must distinguish transient failures from persistent unavailability.

## 45. Circuit Breaking

High-volume paths may use resilience mechanisms to stop hammering an unavailable Redis deployment.

Fallback behavior must remain explicit.

## 46. Local Development

Redis should be available through local Docker infrastructure.

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

Local development must not require production cloud Redis.

## 47. Testing

Redis-related tests should include:

```text
cache hit
cache miss
expiration
serialization
event-driven projection update
duplicate event processing
Redis unavailable
fallback behavior
key version migration
```

## 48. Integration Tests

Use real Redis through Testcontainers where practical.

Do not rely exclusively on mocks for Redis behavior such as TTL and atomic operations.

## 49. Rebuilding Projections

Event-driven Redis projections must have a rebuild strategy.

Potential sources:

```text
canonical database
Kafka replay
snapshot + event catch-up
```

The exact strategy depends on the projection.

## 50. Warm-Up

Critical read projections may require controlled warm-up after deployment or data loss.

Warm-up must be bounded and observable.

Do not overload canonical databases during mass cache reconstruction.

## 51. Cache Consistency

The platform explicitly accepts that cache/projection data may briefly lag canonical state.

Where strong consistency is required, query the authoritative capability instead of relying on Redis.

## 52. Multi-Region Considerations

Future multi-region deployment may require decisions around:

```text
regional Redis instances
replication
latency
consistency
failover
data locality
```

These decisions are deferred until multi-region requirements exist.

## 53. Consequences

### Positive

```text
very low read latency
reduced canonical database load
distributed cache across application instances
high-throughput availability reads
efficient short-lived data
support for rate limiting and technical coordination
```

### Negative

```text
additional infrastructure
cache invalidation complexity
eventual consistency
memory cost
operational monitoring
risk of stale data
projection rebuild requirements
```

These tradeoffs are accepted.

## 54. Alternatives Considered

### In-Memory Cache Only

Rejected as the primary distributed caching strategy because multiple application instances require shared cache state.

Local in-memory caching may still be used for small process-local data where appropriate.

### Canonical Database for Every Read

Rejected for high-volume latency-sensitive workloads because it can create unnecessary load and slower response times.

### Elasticsearch as General Cache

Rejected because Elasticsearch serves Search/read projection workloads and should not replace Redis's low-latency cache semantics.

### Redis as Canonical Database for Everything

Rejected because the platform requires durable Domain ownership and polyglot persistence appropriate to each context.

## 55. Architecture Enforcement

The decision should be enforced through:

```text
ports and adapters
project references
architecture tests
cache contracts
event-driven projection consumers
observability
code review
Copilot instructions
```

## 56. Copilot Rules

GitHub Copilot must:

```text
Never reference Redis packages from Domain.

Never treat Redis as canonical commerce state unless explicitly approved by ADR.

Never serialize Domain Aggregates directly into Redis by default.

Use provider-neutral ports.

Use purpose-specific read-store abstractions for complex projections.

Use explicit TTL policies.

Use event-driven updates for important derived projections.

Do not create unsafe database + Redis dual writes.

Handle cache misses.

Handle Redis unavailability.

Use CancellationToken where APIs permit.

Use bounded timeouts.

Avoid unbounded retries.

Add telemetry for cache behavior.

Version incompatible cache schemas/keys.

Keep Redis configuration in Infrastructure/Host layers.
```

## 57. Initial Implementation Sequence

Recommended order:

```text
1. Add Redis to local Docker Compose

2. Create Redis Infrastructure registration

3. Define serialization/key conventions

4. Implement basic distributed-cache abstraction if required

5. Implement Availability projection consumer

6. Consume AvailabilityChanged from Kafka

7. Write national/regional Availability projections

8. Add low-latency Availability query path

9. Add cache/projection telemetry

10. Add Redis failure/fallback tests

11. Evaluate Pricing cache from measured demand

12. Evaluate Freight quote cache
```

## 58. First Redis Vertical Slice

The first complete Redis flow should be:

```text
Availability Update
       │
       ▼
Availability Domain
       │
       ▼
Canonical MongoDB + Outbox
       │
       ▼
Kafka
       │
       ▼
Availability Projection Worker
       │
       ▼
Redis
       │
       ▼
Availability Query
```

This validates Redis as a derived low-latency projection without compromising canonical ownership.

## 59. Relationship to Other ADRs

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
ADR-0007
Use Elasticsearch for Search Projections

ADR-0009
Cloud Provider Strategy
```

## 60. Final Decision

Yunu.Commerce adopts Redis as its primary distributed cache and low-latency projection technology.

Redis accelerates commerce reads and supports selected ephemeral distributed concerns while remaining outside the canonical Domain model.

The defining principle is:

> Redis makes Yunu.Commerce faster, not authoritative. Canonical business truth remains inside the owning Bounded Context.
