# Yunu.Commerce - Availability Domain

## 1. Purpose

This document defines the Availability Bounded Context for Yunu.Commerce.

The Availability Domain owns the canonical business representation of whether a commercial item can be supplied from eligible fulfillment locations and scopes.

Availability answers the question:

> Is this Offer available, where is it available, and in what quantity can it be supplied?

The platform must support:

- National availability
- Regional availability
- Branch availability
- Store availability
- Warehouse availability
- Fulfillment-node availability
- Available quantity
- Sellable quantity
- Availability status
- High-volume availability updates

Availability does not own:

- Product descriptive information
- SKU structure
- Seller lifecycle
- Offer lifecycle
- Product price
- Payment prices
- Fulfillment-node master data
- Freight calculation
- Search indexes
- AI provider implementation

---

# 2. Domain Responsibility

Availability is responsible for:

- Availability state
- Available quantity
- Sellable quantity
- Availability scope
- National availability
- Regional availability
- Fulfillment-node availability
- Availability resolution
- Availability lifecycle
- Availability business rules
- Availability change facts

Availability must remain independent from:

- database technology
- Kafka
- Redis
- Elasticsearch
- Azure
- Google Cloud
- ERP schemas
- WMS schemas
- marketplace schemas

---

# 3. Core Ubiquitous Language

The initial Availability language includes:

```text
Availability
AvailabilityId
AvailableQuantity
SellableQuantity
AvailabilityStatus
AvailabilityScope
NationalAvailability
RegionalAvailability
FulfillmentNodeAvailability
RegionId
FulfillmentNodeId
OfferId
SkuId
StockPosition
Reservation
SafetyStock
```

Not every candidate concept must exist in the first implementation.

---

# 4. Availability

Availability represents the business state indicating whether an item can participate in commerce within a defined scope.

Conceptually:

```text
Availability
│
├── AvailabilityId
├── Commercial Reference
├── Scope
├── Quantity
├── Status
└── UpdatedAtUtc
```

The exact commercial reference must be explicitly decided before implementation.

---

# 5. Availability Is Not Catalog

Catalog answers:

> What is the SKU?

Availability answers:

> Can it currently be supplied?

Catalog must not contain mutable stock or regional availability state.

---

# 6. Availability Is Not Offer

Offer answers:

> Which Seller commercially offers this SKU?

Availability answers:

> Can that commercial item currently be supplied?

An Active Offer does not imply Availability.

---

# 7. Availability Is Not Fulfillment

Fulfillment owns:

```text
Store
Branch
Warehouse
Distribution Center
Fulfillment Node
Capabilities
```

Availability owns the supply state associated with those nodes.

Conceptually:

```text
Fulfillment
    │
    └── FulfillmentNodeId
              │
              ▼
        Availability
```

---

# 8. Availability Is Not Freight

Availability determines whether supply exists.

Freight determines how that supply can reach a destination.

Conceptually:

```text
Availability
      │
      ▼
Eligible Supply Nodes
      │
      ▼
Freight
      │
      ▼
Delivery Options
```

---

# 9. Commercial Availability Key

A fundamental modeling decision is what is being made available.

Potential key:

```text
OfferId
+
FulfillmentNodeId
```

This naturally captures:

```text
Seller
+
SKU
+
commercial relationship
+
physical fulfillment source
```

because Offer already references Seller and SKU.

The final key must be validated against 1P/3P inventory requirements.

---

# 10. SKU-Based Availability

Some inventory sources may report stock primarily by:

```text
SkuId
+
FulfillmentNodeId
```

rather than OfferId.

This is especially plausible for 1P inventory.

The architecture must distinguish:

```text
physical stock identity
```

from:

```text
commercial availability identity
```

rather than forcing them to be identical.

---

# 11. Stock Position

StockPosition may represent the physical quantity known at a fulfillment location.

Conceptually:

```text
StockPosition

SkuId
FulfillmentNodeId
OnHandQuantity
UpdatedAtUtc
```

Whether StockPosition belongs directly to Availability or a future Inventory Bounded Context must be evaluated as requirements grow.

---

# 12. Inventory Boundary

Availability and Inventory are related but not necessarily identical.

Inventory answers questions such as:

> How much physical stock exists?

Availability answers:

> How much can currently be sold?

Conceptually:

```text
Physical Stock
     │
     ▼
Inventory Rules
     │
     ▼
Sellable Quantity
     │
     ▼
Availability
```

For the initial Yunu.Commerce architecture, these concerns may begin together if requirements are simple.

They must remain conceptually distinct.

---

# 13. On-Hand Quantity

OnHandQuantity represents known physical stock before business adjustments.

Example:

```text
OnHandQuantity = 10
```

This does not necessarily mean 10 units are sellable.

---

# 14. Reserved Quantity

ReservedQuantity may represent units already committed.

Example:

```text
OnHandQuantity = 10
ReservedQuantity = 3
```

Potential simple calculation:

```text
AvailableQuantity = 7
```

However, reservation semantics must be explicitly defined before implementation.

---

# 15. Safety Stock

SafetyStock represents inventory intentionally withheld from customer-facing availability.

Example:

```text
OnHandQuantity = 10
ReservedQuantity = 2
SafetyStock = 1

SellableQuantity = 7
```

The exact calculation is business behavior and must not be buried in persistence queries.

---

# 16. Sellable Quantity

SellableQuantity represents the quantity currently eligible for commerce after applicable inventory policies.

Conceptually:

```text
SellableQuantity
=
OnHand
- Reserved
- SafetyStock
± applicable adjustments
```

This formula is illustrative only.

The final calculation must be approved from real requirements.

---

# 17. Quantity Invariants

Potential invariants include:

```text
quantities must not be negative unless explicitly supported
reserved quantity must follow reservation rules
sellable quantity must be deterministic
```

The platform must explicitly decide how overselling and negative inventory are handled.

---

# 18. Availability Status

Potential availability states include:

```text
Available
Unavailable
Limited
Unknown
```

Whether these are persisted or derived from quantity and rules must be decided during modeling.

---

# 19. Available

A commercial item is Available when the applicable business rules determine that it can be supplied.

This may depend on more than:

```text
Quantity > 0
```

Future rules may include:

```text
fulfillment-node status
seller eligibility
inventory policy
region eligibility
channel
```

Avoid hardcoding simplistic assumptions prematurely.

---

# 20. Unavailable

Unavailable means the item cannot currently be supplied in the requested scope.

Potential causes include:

```text
zero sellable stock
disabled fulfillment node
commercial restriction
inventory policy
regional restriction
```

The exact cause may be useful for internal diagnostics.

---

# 21. Unknown Availability

Distributed systems occasionally lose freshness or connectivity.

An explicit:

```text
Unknown
```

state may be safer than incorrectly treating missing information as:

```text
Available
```

or:

```text
Unavailable
```

The final behavior must be defined according to commerce requirements.

---

# 22. National Availability

National Availability answers:

> Is this commercial item available somewhere in the national commerce network?

Conceptually:

```text
Offer
  │
  ├── Node SP = Available
  ├── Node PR = Unavailable
  └── Node BA = Available
           │
           ▼
National Availability = Available
```

National availability may be a projection derived from lower-level state rather than an independent source of truth.

---

# 23. Regional Availability

Regional Availability answers:

> Is this item available for the requested region?

Conceptually:

```text
Region Southeast

├── Node SP-01
├── Node SP-02
└── Node RJ-01
       │
       ▼
Regional Availability
```

The mapping between Regions and Fulfillment Nodes belongs to an explicit boundary.

---

# 24. Fulfillment-Node Availability

The most granular initial availability representation may be:

```text
Offer/SKU
+
FulfillmentNodeId
+
Quantity
```

Higher-level availability can then be derived or projected.

---

# 25. Branch Availability

Branches are Fulfillment Nodes with specific capabilities.

Example:

```text
Branch 1001
SKU 123
SellableQuantity = 4
```

Availability owns the quantity/state.

Fulfillment owns what Branch 1001 is.

---

# 26. Store Availability

A physical Store may support:

```text
ship-from-store
pickup
local delivery
```

Availability must not assume every Store has every fulfillment capability.

Fulfillment provides those capabilities.

---

# 27. Warehouse Availability

Warehouses and distribution centers may hold larger stock pools.

Availability should use canonical FulfillmentNodeId rather than embedding warehouse implementation details into the Aggregate.

---

# 28. Region

Availability may require:

```text
RegionId
```

to resolve regional views.

Region must be canonical and not represented by arbitrary free-form strings when business identity matters.

---

# 29. Geographic Hierarchy

Future geographic resolution may include:

```text
Country
State
Region
City
Postal Code
```

Do not place the entire geographic model inside Availability unless actual rules require it.

---

# 30. National vs Regional Precedence

Availability is not necessarily a simple override model like Pricing.

Regional Availability may be calculated from eligible nodes serving the requested region.

Conceptually:

```text
Destination
    │
    ▼
Region Resolution
    │
    ▼
Eligible Fulfillment Nodes
    │
    ▼
Availability
```

This interaction with Fulfillment and Freight must be carefully separated.

---

# 31. Availability Resolution

Conceptually:

```text
Offer/SKU
+
Destination or Region
+
Current Context
       │
       ▼
Availability Resolution
       │
       ▼
Eligible Availability
```

The exact resolution responsibilities will be refined with Fulfillment and Freight.

---

# 32. Source Systems

Availability updates may originate from:

```text
ERP
WMS
Store systems
OMS
Marketplace
Inventory service
Manual operations
```

External source schemas must not become the canonical Domain model.

---

# 33. Anti-Corruption Layer

External stock models must pass through adapters.

Conceptually:

```text
WMS Stock
ERP Stock
Store Stock
Marketplace Stock
      │
      ▼
Integration Adapter
      │
      ▼
Anti-Corruption Layer
      │
      ▼
Canonical Availability Input
      │
      ▼
Availability Application
```

---

# 34. High Update Frequency

Availability is expected to be a high-throughput context.

Stock may change frequently because of:

```text
sales
reservations
cancellations
returns
receiving
transfers
manual adjustments
synchronization
```

Architecture must support frequent updates without contaminating Domain design with broker-specific concerns.

---

# 35. Event-Driven Availability

Availability is a strong EDA candidate.

Conceptually:

```text
Inventory Source
      │
      ▼
Kafka
      │
      ▼
Availability Consumer
      │
      ▼
Availability Application
      │
      ▼
Canonical Availability State
```

---

# 36. Ordering

Events for the same stock identity may require ordering.

Potential partition/business keys include:

```text
SkuId + FulfillmentNodeId
```

or:

```text
OfferId + FulfillmentNodeId
```

The final Kafka strategy belongs to Integration Architecture.

---

# 37. Idempotency

Availability consumers must tolerate duplicate event delivery.

Potential mechanisms include:

```text
MessageId
SourceVersion
SequenceNumber
Inbox pattern
Idempotency key
```

The Domain must not depend on Kafka delivery semantics.

---

# 38. Stale Events

Out-of-order inventory events can corrupt Availability.

The system may need:

```text
source version
event timestamp
sequence number
logical version
```

to reject stale updates.

The exact strategy must be defined per integration source.

---

# 39. Eventual Consistency

Availability is naturally eventually consistent across distributed systems.

Example:

```text
WMS Stock Changed
      │
      ▼
Kafka
      │
      ▼
Availability updated
      │
      ▼
Redis updated
      │
      ▼
Search projection updated
```

Each stage may have a small propagation delay.

---

# 40. Freshness

Availability freshness is commercially important.

The architecture should eventually expose observability around:

```text
last source update
last processed event
consumer lag
projection lag
cache freshness
```

These are operational concerns, not Domain behavior.

---

# 41. Redis

Redis is a strong candidate for high-volume Availability reads.

Conceptually:

```text
Canonical Availability State
          │
          ▼
Availability Projection / Cache
          │
          ▼
Redis
```

Redis can support very low-latency customer-facing reads.

---

# 42. Redis Is Not Domain

Availability Domain must not know:

```text
Redis keys
Redis TTL
Redis cluster topology
serialization format
```

These belong to Infrastructure.

---

# 43. Cache Failure

The architecture must define behavior when Redis is unavailable.

Potential strategies may include:

```text
fallback to canonical store
degraded response
temporary unavailable state
```

The final strategy depends on latency and reliability requirements.

---

# 44. Canonical Persistence

Availability may require a canonical persistence store for:

```text
latest known state
audit
recovery
rebuilding projections
integration reconciliation
```

Potential stores include relational or document databases.

The decision belongs to Data Architecture.

---

# 45. MongoDB Candidate

Availability data may fit MongoDB well when modeled around high-volume stock/availability documents and flexible projections.

Potential advantages include:

```text
high write throughput
document-oriented stock state
horizontal scaling options
flexible indexing
```

This is a candidate, not yet a final architecture decision.

---

# 46. Relational Candidate

Relational persistence may also be appropriate where:

```text
strict constraints
transactional inventory
reservations
auditing
relational reconciliation
```

are dominant.

The final choice must follow use cases rather than fashion.

---

# 47. Search Boundary

Search may include a simplified availability projection.

Example search document fields:

```text
isAvailable
availableRegions
pickupAvailable
```

Search must not become the authoritative Availability source.

---

# 48. Search Update Flow

Conceptually:

```text
AvailabilityChanged
       │
       ▼
Kafka
       │
       ▼
Search Consumer
       │
       ▼
Elasticsearch
```

High-frequency updates must be designed carefully to avoid unnecessary indexing pressure.

---

# 49. Search Projection Optimization

Not every stock quantity change needs to reindex a Product if customer-visible search state did not change.

Example:

```text
Quantity 100 → 99
isAvailable remains true
```

A projection strategy may suppress unnecessary search updates.

This optimization belongs outside the core Domain.

---

# 50. Pricing Boundary

Availability does not own Price.

Conceptually:

```text
Offer
├── Pricing → how much?
└── Availability → can it be supplied?
```

These concerns must remain independent.

---

# 51. Sellers Boundary

Seller lifecycle belongs to Sellers.

Availability may need Seller eligibility as an input to a higher-level commerce decision, but it must not own Seller status.

---

# 52. Offers Boundary

Offer lifecycle belongs to Offers.

Availability may reference:

```text
OfferId
```

but must not activate or deactivate the Offer directly.

---

# 53. Fulfillment Boundary

Fulfillment defines:

```text
which nodes exist
what type they are
where they are
what capabilities they have
```

Availability defines:

```text
what supply state exists at those nodes
```

---

# 54. Freight Boundary

Freight may consume available Fulfillment Nodes to calculate delivery.

Conceptually:

```text
Availability
      │
      ▼
Available Nodes
      │
      ▼
Freight
      │
      ▼
Delivery Options
```

Availability does not calculate delivery price or SLA.

---

# 55. Reservation

Reservation may temporarily reduce sellable stock.

Potential conceptual flow:

```text
Available Stock
      │
      ▼
Reservation
      │
      ▼
Reduced Sellable Quantity
```

However, reservation often becomes complex enough to justify a dedicated Inventory/Reservation model.

Do not implement reservation deeply in the initial Availability slice.

---

# 56. Overselling

The platform must eventually define an explicit overselling policy.

Possible strategies include:

```text
never allow negative sellable quantity
allow controlled oversell
seller-specific policy
SKU-specific policy
```

This is a business decision, not an Infrastructure default.

---

# 57. Safety Stock Policy

Safety stock may vary by:

```text
SKU
Seller
Fulfillment Node
Category
Region
```

If these rules become complex, they may require dedicated policy modeling.

Avoid embedding arbitrary condition trees inside Availability prematurely.

---

# 58. Aggregate Root Candidate

Potential Aggregate Root:

```text
Availability
```

A candidate identity may represent:

```text
CommercialItem
+
FulfillmentNode
```

The exact boundary depends on write concurrency and source data.

---

# 59. Small Aggregate Principle

Do not model:

```text
SKU
└── all availability for every branch in Brazil
```

as one enormous Aggregate.

Availability should support independent high-concurrency updates.

---

# 60. Value Object Candidates

Potential Value Objects include:

```text
AvailabilityId
Quantity
RegionId
FulfillmentNodeId
StockVersion
AvailabilityStatus
```

Strongly typed cross-context identifiers may be locally represented without referencing foreign Domain projects.

---

# 61. Repository Boundary

Potential repository contract:

```text
IAvailabilityRepository
```

Repository contracts must not expose:

```text
MongoCollection
DbContext
SQL Connection
Redis client
Kafka consumer
```

---

# 62. Application Use Cases

Potential use cases include:

```text
UpdateAvailability
AdjustAvailability
SetAvailability
GetAvailability
GetNationalAvailability
GetRegionalAvailability
GetAvailabilityByFulfillmentNode
GetAvailableFulfillmentNodes
```

Future inventory capabilities may include:

```text
ReserveStock
ReleaseReservation
ConfirmReservation
```

only when explicitly designed.

---

# 63. CQRS

Availability is a strong candidate for CQRS.

Write side:

```text
inventory updates
adjustments
synchronization
```

Read side:

```text
national availability
regional availability
node availability
customer-facing availability
```

Read models may be aggressively optimized.

---

# 64. Domain Events

Potential Domain Events include:

```text
AvailabilityCreatedDomainEvent
AvailabilityChangedDomainEvent
ItemBecameAvailableDomainEvent
ItemBecameUnavailableDomainEvent
SellableQuantityChangedDomainEvent
```

Exact events must emerge from real Domain behavior.

---

# 65. Integration Events

Potential Integration Events include:

```text
AvailabilityChanged
ItemBecameAvailable
ItemBecameUnavailable
RegionalAvailabilityChanged
```

Consumers may include:

```text
Search
Commerce read models
Analytics
External integrations
```

---

# 66. Semantic Events

For downstream systems, semantic events may be more useful than every raw quantity mutation.

Example:

```text
Quantity 8 → 7
```

may not matter to Search.

But:

```text
Quantity 1 → 0
```

may produce:

```text
ItemBecameUnavailable
```

This distinction can dramatically reduce downstream event noise.

---

# 67. Transactional Outbox

Where Availability changes are persisted transactionally, Integration Events should use an Outbox strategy where appropriate.

Conceptually:

```text
Availability update
       │
       ├── Persist state
       └── Persist Outbox
                │
                ▼
              Kafka
```

---

# 68. Inbox Pattern

Consumers processing stock/inventory events should support Inbox/idempotency behavior.

Duplicate delivery must not duplicate quantity changes accidentally.

This is particularly important for delta-based events.

---

# 69. Absolute vs Delta Updates

External inventory systems may send:

```text
absolute quantity
```

or:

```text
quantity delta
```

Example:

```text
Absolute:
Quantity = 12

Delta:
Quantity += -1
```

These semantics must never be confused.

The integration contract must make the distinction explicit.

---

# 70. Absolute Updates

Absolute updates are often easier to make idempotent.

Example:

```text
Set SKU 123 / Node 10 quantity to 12
```

Repeated processing produces the same result.

---

# 71. Delta Updates

Delta updates require stronger duplicate and ordering protection.

Example:

```text
Decrease quantity by 1
```

Processing twice incorrectly reduces stock twice.

Use delta semantics only with explicit delivery guarantees and idempotency controls.

---

# 72. Reconciliation

Distributed inventory systems require reconciliation.

Potential workflow:

```text
Streaming updates
      +
Periodic snapshot
      │
      ▼
Reconciliation
      │
      ▼
Correct canonical state
```

This protects against lost, stale or corrupted event streams.

---

# 73. Availability Snapshot

External systems may periodically provide full inventory snapshots.

Snapshot processing must be designed separately from incremental event processing.

Large snapshots should not force loading enormous Domain graphs.

---

# 74. Concurrency

Availability is highly concurrency-sensitive.

Potential concurrent operations include:

```text
sale
reservation
cancellation
stock receipt
transfer
manual correction
WMS synchronization
```

Concurrency strategy must be explicit.

---

# 75. Optimistic Concurrency

Potential mechanisms include:

```text
Version
SourceVersion
ETag
database concurrency token
```

The Domain should understand business version semantics only when meaningful.

---

# 76. Pessimistic Locking

Pessimistic locking may be inappropriate for many high-throughput distributed availability flows.

If used, it must be justified by specific consistency requirements.

Do not choose it by default.

---

# 77. Partitioning

Availability data may eventually require partitioning by stable high-cardinality keys.

Potential examples:

```text
SkuId
OfferId
FulfillmentNodeId
```

The final database and Kafka partition strategies belong to Infrastructure and Data Architecture.

---

# 78. Auditability

Availability changes may require metadata such as:

```text
UpdatedAtUtc
Source
SourceVersion
CorrelationId
Reason
```

Detailed inventory history may be stored separately from current availability state.

---

# 79. Availability History

Current state and historical movement are different models.

Conceptually:

```text
Current Availability
```

optimized for fast reads.

```text
Inventory/Availability History
```

optimized for audit and investigation.

Do not force all history into the active Aggregate.

---

# 80. Observability

Availability requires strong operational telemetry.

Important metrics may include:

```text
event processing rate
consumer lag
stale update rejection
availability update latency
Redis hit rate
projection lag
reconciliation differences
```

OpenTelemetry instrumentation belongs outside Domain.

---

# 81. Error Semantics

Potential meaningful errors include:

```text
AvailabilityNotFound
InvalidQuantity
StaleAvailabilityUpdate
InvalidFulfillmentNodeReference
InvalidAvailabilityState
DuplicateInventoryEvent
```

Infrastructure exceptions must not leak directly into business APIs.

---

# 82. Validation Layers

Application validation may verify:

```text
required identifiers
input format
message shape
```

Domain validation protects:

```text
quantity invariants
state transitions
availability rules
```

Infrastructure validation protects:

```text
database constraints
serialization
provider-specific requirements
```

---

# 83. Security

Availability mutations may originate only from trusted actors such as:

```text
WMS integration
ERP integration
OMS
internal inventory service
authorized operator
```

Authorization belongs to Application/Host boundaries.

Domain logic must not depend on ASP.NET security APIs.

---

# 84. Testing Strategy

Availability Domain tests should focus on:

```text
quantity behavior
availability transitions
available/unavailable semantics
stale update rules
absolute vs delta semantics
regional resolution rules
Domain Events
```

Integration tests should cover high-risk infrastructure behavior separately.

---

# 85. Architecture Questions Before Implementation

Before implementing Availability, explicitly decide:

```text
Is canonical availability keyed by OfferId or SkuId?

How does 1P stock differ from 3P stock?

Is physical Inventory part of this context initially?

What exactly is SellableQuantity?

Do reservations belong here?

Is SafetyStock required initially?

Can quantity become negative?

What is the overselling policy?

How is National Availability derived?

How is Regional Availability derived?

What defines a Region?

How are Fulfillment Nodes mapped to Regions?

What happens when Fulfillment Node is disabled?

Are incoming updates absolute or delta?

How are stale events detected?

What ordering guarantees are required?

What is the canonical persistence store?

What data belongs in Redis?

What availability state belongs in Elasticsearch?

What latency is acceptable from stock change to customer visibility?
```

These decisions must be explicit before detailed implementation.

---

# 86. Initial Implementation Scope

The first Availability implementation should remain intentionally small.

Recommended initial slice:

```text
Availability
AvailabilityId
SkuId or OfferId
FulfillmentNodeId
Quantity
AvailabilityStatus
SourceVersion
basic absolute availability update
available/unavailable transition
Domain Events
repository port
unit tests
```

Do not implement complex reservations, safety-stock policies or distributed inventory transactions in the first slice.

---

# 87. Relationship with Catalog

Catalog answers:

> What is the SKU?

Availability references canonical SKU identity where required.

Catalog never owns current stock.

---

# 88. Relationship with Sellers

Sellers answers:

> Who is selling?

Availability may ultimately be associated with Seller commerce through Offer.

Seller lifecycle remains outside Availability.

---

# 89. Relationship with Offers

Offers answers:

> What Seller-SKU commercial relationship exists?

Availability answers:

> Can that commercial item be supplied?

The final key relationship will be explicitly decided.

---

# 90. Relationship with Pricing

Pricing answers:

> How much does it cost?

Availability answers:

> Can it be supplied?

Neither should directly own the other's state.

---

# 91. Relationship with Fulfillment

Fulfillment answers:

> What fulfillment locations exist and what can they do?

Availability answers:

> What supply state exists at those locations?

This is a critical boundary.

---

# 92. Relationship with Freight

Freight answers:

> Which available fulfillment option can serve the destination, at what cost and SLA?

Availability provides supply eligibility inputs.

It does not calculate delivery.

---

# 93. Relationship with Search

Search may expose simplified availability to customers.

Example:

```text
isAvailable = true
```

or:

```text
availableForPickup = true
```

These are projections.

Availability remains authoritative.

---

# 94. Customer Availability Read Model

A customer-facing read model may combine:

```text
Offer
Price
Region
Availability
Fulfillment options
```

This composite read model is not the Availability Aggregate.

---

# 95. Buyability

Availability is one component of buyability.

Conceptually:

```text
Buyable
=
Active SKU
+
Active Seller
+
Active Offer
+
Applicable Price
+
Availability
+
Eligible Fulfillment
```

Availability must not absorb the entire commerce decision.

---

# 96. Data Ownership

Availability is authoritative for:

```text
availability state
sellable quantity
availability per owned scope
availability transitions
```

Other contexts must not directly modify Availability persistence.

---

# 97. No Shared Database Ownership

Even if Availability shares database infrastructure with another module, ownership remains explicit.

Forbidden:

```text
Freight updating Availability records
```

Forbidden:

```text
Search correcting Availability source data
```

Communication occurs through explicit contracts and events.

---

# 98. Domain Purity

Availability Domain must not reference:

```text
ASP.NET Core
Entity Framework
Dapper
MongoDB.Driver
Kafka
Redis
Elasticsearch
Azure SDK
Google Cloud SDK
OpenTelemetry
HTTP clients
```

The Domain must remain independently testable.

---

# 99. Evolution Principle

Availability will evolve as real inventory and fulfillment requirements become clear.

Avoid prematurely implementing:

```text
distributed reservation engine
ATP/CTP engine
inventory forecasting
allocation engine
warehouse transfer planning
machine-learning inventory optimization
complex safety-stock policies
```

These may later justify separate capabilities or Bounded Contexts.

---

# 100. Core Rule

Availability owns the canonical answer to:

> Can this commercial item currently be supplied, and from which eligible inventory scope?

It does not answer:

```text
What is the product?        -> Catalog
Who is selling it?          -> Sellers
What is the Offer?          -> Offers
How much does it cost?      -> Pricing
What fulfillment nodes exist? -> Fulfillment
How is it delivered?        -> Freight
```

---

# 101. Final Principle

The Availability Domain protects the high-frequency commerce state that determines whether supply exists.

It must remain:

```text
high-throughput
deterministic
idempotent
concurrency-aware
event-friendly
fulfillment-aware but fulfillment-independent
pricing-independent
search-independent
cache-independent
database-independent
broker-independent
cloud-independent
AI-provider-independent
```

Inventory sources may change.

Persistence may change.

Redis topology may change.

Kafka topology may change.

The Availability business semantics must remain protected.
