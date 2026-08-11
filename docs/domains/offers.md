# Yunu.Commerce - Offers Domain

## 1. Purpose

This document defines the Offers Bounded Context for Yunu.Commerce.

The Offers Domain owns the commercial relationship between a canonical SKU and a Seller.

Offers answers the question:

> What is this Seller offering for this SKU?

An Offer represents the commercial proposition that makes a Catalog SKU sellable by a specific Seller.

Offers does not own:

- Product descriptive data
- SKU descriptive data
- Seller lifecycle
- Price calculation
- Payment prices
- Stock quantities
- Regional availability
- Fulfillment topology
- Freight calculation
- Search indexes
- AI provider implementations

Those concerns belong to other Bounded Contexts.

---

# 2. Domain Responsibility

Offers is responsible for:

- Offer identity
- Seller-to-SKU commercial relationship
- Offer lifecycle
- Offer activation
- Offer deactivation
- Offer suspension
- Offer publication eligibility
- First-party and third-party offer representation
- External offer references
- Commercial relationship status
- Offer-level business invariants

Offers must preserve a canonical business model independent from:

- ERP systems
- marketplace schemas
- external commerce platforms
- databases
- Kafka
- Redis
- Elasticsearch
- cloud providers
- AI providers

---

# 3. Core Ubiquitous Language

The initial Offers language includes:

```text
Offer
OfferId
SellerId
SkuId
OfferStatus
OfferType
ExternalOfferReference
CommercialOffer
OfferActivation
OfferSuspension
```

External terminology must be translated into this canonical language before entering the Domain.

---

# 4. Offer

An Offer represents the commercial relationship between:

```text
Seller
+
SKU
```

Conceptually:

```text
Offer
│
├── OfferId
├── SellerId
├── SkuId
├── Status
└── External References
```

An Offer does not duplicate the Product or Seller Aggregate.

---

# 5. Canonical Relationship

The fundamental relationship is:

```text
Catalog SKU
     │
     │ SkuId
     ▼
   Offer
     ▲
     │ SellerId
     │
   Seller
```

The Offer connects two independently owned concepts.

Catalog owns the SKU.

Sellers owns the Seller.

Offers owns the commercial relationship.

---

# 6. Multiple Sellers per SKU

The same canonical SKU may have multiple Offers.

Example:

```text
SKU: iPhone 17 Pro 256 GB Black

├── Offer A
│   Seller = Yunu 1P
│
├── Offer B
│   Seller = Marketplace Seller A
│
└── Offer C
    Seller = Marketplace Seller B
```

This is fundamental to marketplace commerce.

---

# 7. One Seller with Multiple Offers

A Seller may have many Offers.

Conceptually:

```text
Seller A

├── Offer for SKU 100
├── Offer for SKU 200
├── Offer for SKU 300
└── ...
```

The Seller Aggregate must not contain all Offers.

Offers are independently managed within the Offers Bounded Context.

---

# 8. Offer Identity

Every Offer must have a canonical Yunu.Commerce identity:

```text
OfferId
```

External marketplace or ERP identifiers must not replace OfferId.

Potential external identifiers include:

```text
Marketplace Offer Id
ERP Offer Code
Legacy Offer Id
Seller Listing Id
Partner Offer Id
```

---

# 9. External Offer Reference

External Offer identifiers may be represented conceptually as:

```text
ExternalOfferReference

System
Type
Value
```

Example:

```text
System = MarketplaceA
Type = ListingId
Value = ABC-12345
```

External references exist for integration traceability.

They do not define internal Domain identity.

---

# 10. Offer Uniqueness

A canonical business rule may require uniqueness for the relationship:

```text
SellerId + SkuId
```

However, this must be validated against future requirements.

Some commerce models may allow multiple commercial Offers for the same Seller/SKU combination based on:

```text
condition
channel
contract
fulfillment model
commercial program
```

Therefore the exact uniqueness key must be explicitly decided before implementation.

---

# 11. Offer Type

Offer type may initially reflect commercial ownership.

Potential values:

```text
FirstParty
ThirdParty
```

However, OfferType must not unnecessarily duplicate SellerType.

If Offer ownership can always be derived safely from Seller, OfferType may not be required.

This decision must be made during detailed modeling.

---

# 12. First-Party Offer - 1P

A 1P Offer represents a commercial Offer where the platform/retailer is the Seller.

Conceptually:

```text
Seller.Type = FirstParty
```

The Offer still remains separate from Catalog.

Example:

```text
Product
  ↓
SKU
  ↓
Offer
  ↓
Seller = 1P
```

---

# 13. Third-Party Offer - 3P

A 3P Offer represents a commercial Offer from an external Seller.

Conceptually:

```text
Seller.Type = ThirdParty
```

Multiple 3P Sellers may offer the same SKU.

---

# 14. Offer Status

Offer lifecycle must be explicit.

Potential initial statuses:

```text
Draft
Active
Inactive
Suspended
Archived
```

Exact transitions will be refined from business requirements.

---

# 15. Draft Offer

A Draft Offer exists but is not yet commercially active.

Potential reasons include:

```text
initial creation
incomplete configuration
pending validation
pending seller activation
pending catalog validation
```

Draft does not imply sellability.

---

# 16. Active Offer

An Active Offer is commercially enabled from the Offers Domain perspective.

However:

```text
Active Offer != Buyable Item
```

Actual customer buyability may additionally depend on:

```text
Seller status
SKU status
Price
Availability
Fulfillment
Regional rules
Channel rules
```

This distinction is critical.

---

# 17. Inactive Offer

An Inactive Offer exists but is intentionally not commercially active.

It may potentially be reactivated depending on future business rules.

---

# 18. Suspended Offer

A Suspended Offer is temporarily prevented from commercial participation.

Potential causes may eventually include:

```text
Seller suspension
commercial policy
integration issues
manual operation
data inconsistency
```

The exact reasons must be modeled only when requirements demand them.

---

# 19. Archived Offer

Archived represents an Offer that should no longer participate in normal commerce flows but must remain historically traceable.

Historical references may be required by:

```text
orders
analytics
auditing
pricing history
marketplace synchronization
```

Physical deletion should not be the default lifecycle strategy.

---

# 20. Offer State Transitions

Potential conceptual lifecycle:

```text
Draft
  │
  ▼
Active
  │
  ├────────► Suspended
  │              │
  │              ▼
  │            Active
  │
  ├────────► Inactive
  │              │
  │              ▼
  │            Active
  │
  └────────► Archived
```

Exact legal transitions must be approved before implementation.

---

# 21. Catalog Boundary

Offers does not own:

```text
Product
SKU descriptive data
Category
Brand
Attributes
Specifications
Media
```

Offers references Catalog identity using:

```text
SkuId
```

and potentially ProductId in read models where justified.

---

# 22. Sellers Boundary

Offers does not own:

```text
Seller legal identity
Seller onboarding
Seller lifecycle
Seller approval
Seller suspension policy
```

Offers references:

```text
SellerId
```

Seller status is owned by Sellers.

---

# 23. Pricing Boundary

Offer does not own price behavior.

Pricing owns:

```text
Regular Price
Sale Price
Regional Price
PIX Price
Boleto Price
Installment conditions
Promotional Price
```

Conceptually:

```text
OfferId
   │
   ▼
Pricing
```

Pricing decides how prices are represented and calculated.

---

# 24. Price Must Not Live Inside Offer Aggregate

Avoid coupling:

```text
Offer
├── Seller
├── SKU
├── Stock
├── Price
├── PIX Price
├── Regional Prices
└── Freight
```

This would collapse multiple Bounded Contexts into one oversized model.

Preferred:

```text
Offer
├── OfferId
├── SellerId
├── SkuId
└── Offer lifecycle
```

with other capabilities connected through explicit boundaries.

---

# 25. Availability Boundary

Offers does not own:

```text
Stock Quantity
National Availability
Regional Availability
Branch Availability
Inventory Reservation
```

Availability references the appropriate commercial identities.

Potential conceptual relationship:

```text
OfferId
+
FulfillmentNodeId
+
Region
      │
      ▼
Availability
```

The exact availability key will be defined in the Availability Domain.

---

# 26. Fulfillment Boundary

Offer may eventually reference or constrain fulfillment models.

However, Offers does not own:

```text
Warehouse
Store
Branch
Distribution Center
Fulfillment Node inventory
```

Fulfillment owns the fulfillment topology.

---

# 27. Freight Boundary

Offers does not calculate:

```text
Freight price
Delivery SLA
Delivery promise
Carrier selection
Regional freight
```

Freight consumes the necessary commercial and fulfillment information through contracts.

---

# 28. Search Boundary

Search may project Offer information into Elasticsearch.

Example:

```text
OfferActivated
      │
      ▼
Search Projection
      │
      ▼
Elasticsearch
```

Search may combine:

```text
Catalog
Offer
Seller
Pricing
Availability
```

into a denormalized customer-facing document.

This does not transfer ownership of those concepts to Search.

---

# 29. AI Boundary

AI may assist Offer-related workflows in the future.

Potential examples:

```text
Offer anomaly detection
Commercial data normalization
Marketplace mapping suggestions
Offer quality analysis
```

AI must not bypass Offer Domain invariants.

AI provider implementation belongs outside Offers Domain.

---

# 30. Aggregate Root Candidate

The primary Aggregate Root candidate is:

```text
Offer
```

Conceptually:

```text
Offer
│
├── OfferId
├── SellerId
├── SkuId
├── OfferStatus
└── External References
```

The Aggregate should remain intentionally small.

---

# 31. Why Offer Should Be Small

Large commerce platforms may contain:

```text
millions
or
tens of millions
```

of Offers.

Offer lifecycle operations must therefore avoid loading unrelated Catalog, Seller, Pricing or Availability graphs.

Aggregate design must respect scale and transactional boundaries.

---

# 32. Value Object Candidates

Potential Value Objects include:

```text
OfferId
ExternalOfferReference
OfferStatus
```

SellerId and SkuId may be represented as strongly typed identifiers within the Offers context.

Value Objects must have meaningful semantics rather than exist only for ceremony.

---

# 33. Cross-Context Identity

Offers may define local strongly typed representations such as:

```text
SellerId
SkuId
```

without referencing foreign Domain projects.

Forbidden:

```text
Offers.Domain
    →
Sellers.Domain.Seller
```

Forbidden:

```text
Offers.Domain
    →
Catalog.Domain.Sku
```

---

# 34. Domain Service Candidates

Domain Services should only exist when real Offer business behavior cannot naturally belong to the Offer Aggregate.

Avoid generic classes such as:

```text
OfferService
OfferManager
```

used merely as containers for procedural logic.

---

# 35. Repository Boundary

Potential repository contract:

```text
IOfferRepository
```

Repository methods should reflect Aggregate persistence needs.

The interface must not expose:

```text
DbContext
MongoCollection
SQL Connection
Redis
Elasticsearch
```

---

# 36. Persistence Independence

Offers Domain must remain independent from database technology.

Potential persistence options include:

```text
SQL Server
PostgreSQL
MongoDB
```

The persistence choice belongs to Data Architecture and Infrastructure.

---

# 37. Data Scale Consideration

Offers may become one of the largest datasets in the commerce platform.

Data architecture must consider:

```text
high cardinality
seller/SKU lookup
bulk updates
marketplace synchronization
status filtering
event throughput
partitioning
index strategy
```

These concerns must not leak into Domain behavior.

---

# 38. Cache Independence

Redis may later optimize Offer reads or computed commerce views.

Redis must not become the canonical Offer source of truth.

Offers Domain must not reference Redis.

---

# 39. Domain Events

Potential Offer Domain Events include:

```text
OfferCreatedDomainEvent
OfferActivatedDomainEvent
OfferDeactivatedDomainEvent
OfferSuspendedDomainEvent
OfferReactivatedDomainEvent
OfferArchivedDomainEvent
```

Exact events must emerge from implemented business behavior.

---

# 40. Integration Events

Potential Integration Events include:

```text
OfferCreated
OfferActivated
OfferUpdated
OfferDeactivated
OfferSuspended
OfferReactivated
OfferArchived
```

Integration Events are contracts for other contexts.

They are not the same objects as Domain Events.

---

# 41. Offer Activation Event

Conceptually:

```text
Offer
  │
  ▼
OfferActivatedDomainEvent
  │
  ▼
Outbox
  │
  ▼
OfferActivated
  │
  ▼
Kafka
```

Interested contexts may consume the event independently.

---

# 42. Seller Suspension Reaction

When Sellers publishes:

```text
SellerSuspended
```

Offers may react.

Conceptually:

```text
SellerSuspended
      │
      ▼
Offers Consumer
      │
      ▼
Application Use Case
      │
      ▼
Affected Offer policy
```

The exact policy must be defined later.

Possible strategies include:

```text
suspend Offers
mark Seller eligibility projection
prevent Offer activation
derive sellability elsewhere
```

Do not invent the final rule before business requirements are defined.

---

# 43. SKU Deactivation Reaction

When Catalog publishes:

```text
SkuDeactivated
```

Offers may need to react.

Again, the exact business response must be explicitly defined.

Offers must not directly query or modify Catalog persistence.

---

# 44. Eventual Consistency

The architecture accepts eventual consistency between Bounded Contexts.

Example:

```text
Seller suspended at T0

Sellers state updated at T0
Offer projection reacts at T0 + Δ
Search projection reacts at T0 + Δ
```

Critical commerce decisions must define where synchronous guarantees are required.

---

# 45. Offer Activation Use Case

Conceptually:

```text
ActivateOfferCommand
       │
       ▼
Offers Application
       │
       ▼
Offer Aggregate
       │
       ▼
Validate transition
       │
       ▼
Active
       │
       ▼
OfferActivatedDomainEvent
```

---

# 46. Offer Creation Use Case

Conceptually:

```text
CreateOfferCommand
      │
      ▼
Offers Application
      │
      ├── Seller reference validation strategy
      ├── SKU reference validation strategy
      │
      ▼
Offer Aggregate
      │
      ▼
Repository
```

Cross-context validation must not be implemented through direct foreign database access.

---

# 47. Application Use Cases

Potential future use cases include:

```text
CreateOffer
UpdateOffer
ActivateOffer
DeactivateOffer
SuspendOffer
ReactivateOffer
ArchiveOffer
GetOfferById
GetOffersBySku
GetOffersBySeller
```

Bulk use cases may later include:

```text
ImportOffers
SynchronizeSellerOffers
SuspendOffersBySeller
```

These should be introduced only when required.

---

# 48. CQRS

Offers Application may use CQRS.

Commands:

```text
CreateOfferCommand
ActivateOfferCommand
SuspendOfferCommand
```

Queries:

```text
GetOfferByIdQuery
GetOffersBySkuQuery
GetOffersBySellerQuery
```

CQRS does not imply separate physical databases.

---

# 49. Bulk Operations

Marketplace commerce may require high-volume operations.

Examples:

```text
bulk Offer import
bulk activation
bulk deactivation
seller synchronization
catalog synchronization
```

Bulk Application workflows must preserve Domain invariants without forcing enormous Aggregates.

---

# 50. Idempotency

Offer creation and external synchronization must support idempotency.

Repeated processing of the same external message must not create duplicate Offers.

Potential mechanisms include:

```text
external reference uniqueness
idempotency keys
Inbox pattern
message identifiers
```

Infrastructure details belong outside Domain.

---

# 51. Transactional Outbox

Integration Events should be published reliably using the Transactional Outbox pattern where appropriate.

Conceptually:

```text
Offer change
     │
     ├── Persist Offer
     │
     └── Persist Outbox Message
              │
              ▼
         Outbox Worker
              │
              ▼
            Kafka
```

This prevents database/event inconsistency.

---

# 52. Inbox Pattern

Consumers handling external integration events should support idempotent processing.

Conceptually:

```text
Kafka Event
    │
    ▼
Inbox Check
    │
    ├── already processed → ignore safely
    │
    └── new → process
```

Inbox implementation belongs to Infrastructure.

---

# 53. Concurrency

Offer state changes may require optimistic concurrency.

Potential strategies include:

```text
Version
ETag
Persistence-level concurrency token
```

The Domain must remain persistence-independent.

---

# 54. Auditability

Offer changes may require:

```text
CreatedAtUtc
UpdatedAtUtc
CreatedBy
UpdatedBy
```

Lifecycle changes may eventually require:

```text
Reason
Source
CorrelationId
```

These should be modeled deliberately rather than added indiscriminately.

---

# 55. Source of Offer

Offers may originate from:

```text
internal administration
ERP
marketplace
seller integration
bulk import
API
event
```

Source metadata may be useful for traceability.

It must not determine Domain architecture.

---

# 56. Channel Consideration

Future commerce requirements may introduce channels such as:

```text
Web
Mobile
Marketplace
B2B
Physical Store
Partner
```

Whether channel belongs inside Offer identity, eligibility or another Bounded Context must be decided from actual use cases.

Do not introduce channel complexity prematurely.

---

# 57. Regional Consideration

Regional commercial differences should not automatically create separate Offers.

Regional price belongs to Pricing.

Regional availability belongs to Availability.

Regional freight belongs to Freight.

Only introduce regional Offer semantics if the commercial Offer itself genuinely differs by region.

---

# 58. Offer vs Listing

External marketplaces often use the concept:

```text
Listing
```

A Listing may map to an Offer, but the terms must not automatically be considered identical.

The Anti-Corruption Layer translates external Listing semantics into Yunu.Commerce Offer semantics.

---

# 59. Anti-Corruption Layer

External Offer structures must pass through adapters.

Conceptually:

```text
Marketplace Listing
       │
       ▼
Integration Adapter
       │
       ▼
Anti-Corruption Layer
       │
       ▼
Canonical Offer Input
       │
       ▼
Offers Application
```

External DTOs must not enter the Domain directly.

---

# 60. Error Semantics

Potential meaningful errors include:

```text
OfferNotFound
OfferAlreadyActive
OfferAlreadySuspended
InvalidOfferState
DuplicateOffer
InvalidSellerReference
InvalidSkuReference
OfferCannotBeActivated
```

Infrastructure exceptions must not leak directly into API semantics.

---

# 61. Validation Layers

Application validation may verify:

```text
required fields
command format
basic input validity
```

Domain validation protects:

```text
Offer invariants
state transitions
commercial relationship consistency
```

Infrastructure validation protects:

```text
database constraints
external provider requirements
serialization
```

---

# 62. Security

Offer management operations may require authorization.

Examples:

```text
internal operator
seller administrator
marketplace integration
system identity
```

Authorization belongs to Application/Host boundaries.

The Domain must not depend on ASP.NET security APIs.

---

# 63. Testing Strategy

Offers Domain tests should focus on:

```text
Offer creation
activation
deactivation
suspension
reactivation
invalid transitions
identity rules
commercial invariants
Domain Events
```

Pure Domain tests should not require infrastructure.

---

# 64. Architecture Questions Before Implementation

Before implementing Offer, explicitly decide:

```text
Is SellerId + SkuId unique?

Can one Seller have multiple Offers for the same SKU?

Can OfferType differ from SellerType?

What makes an Offer eligible for activation?

Does Offer activation require Seller to be Active?

Does Offer activation require SKU to be Active?

Should those checks be synchronous or projection-based?

What happens when Seller is suspended?

What happens when SKU is deactivated?

Can Offers be region-specific?

Can Offers be channel-specific?

How are marketplace Listings mapped?

Which external references must be unique?

What scale and throughput are expected?
```

These decisions must be based on real business requirements.

---

# 65. Initial Implementation Scope

The first Offers implementation should remain small.

Recommended initial slice:

```text
Offer
OfferId
SellerId
SkuId
OfferStatus
ExternalOfferReference
basic creation
activation
deactivation
Domain Events
repository port
unit tests
```

Do not implement Pricing, Availability or Freight inside the Offer Aggregate.

---

# 66. Relationship with Catalog

Catalog answers:

> What is the product/SKU?

Offers references:

```text
SkuId
```

and answers:

> Which Seller commercially offers this SKU?

---

# 67. Relationship with Sellers

Sellers answers:

> Who is the Seller and what is its lifecycle state?

Offers references:

```text
SellerId
```

but does not own Seller.

---

# 68. Relationship with Pricing

Pricing answers:

> How much does this Offer cost and under which payment conditions?

Conceptually:

```text
OfferId
   │
   ▼
Price
```

---

# 69. Relationship with Availability

Availability answers:

> Can this Offer be fulfilled in the requested scope?

Conceptually:

```text
OfferId
   │
   ▼
Availability
```

The exact key model will be defined separately.

---

# 70. Relationship with Fulfillment

Fulfillment answers:

> From which nodes can this Offer be fulfilled?

Offers must not embed fulfillment inventory.

---

# 71. Relationship with Freight

Freight answers:

> How can this Offer be delivered to the customer?

Offers must not calculate delivery costs.

---

# 72. Relationship with Search

Search may build a customer-facing projection combining:

```text
Product
SKU
Offer
Seller
Price
Availability
```

The search document is a projection, not the Domain model.

---

# 73. Buyability

A crucial future concept is whether an Offer is actually buyable.

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
Valid Price
+
Availability
+
Eligible Fulfillment
+
Applicable Commerce Rules
```

This cross-context decision must not be casually placed inside the Offer Aggregate.

The correct orchestration/read-model boundary will be defined later.

---

# 74. Data Ownership

Offers is authoritative for:

```text
Offer identity
Seller-SKU commercial relationship
Offer lifecycle
Offer status
Offer external references
```

Other contexts must not directly modify Offers persistence.

---

# 75. No Shared Database Ownership

Even if modules initially share a database server or cluster:

```text
Catalog
Sellers
Offers
Pricing
Availability
```

must retain explicit data ownership.

Forbidden:

```text
Pricing directly updating Offer records
```

Preferred communication:

```text
Contract
Application API
Integration Event
Projection
```

---

# 76. Domain Purity

Offers Domain must not reference:

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

# 77. Evolution Principle

The Offers model will evolve as real commercial use cases emerge.

Avoid prematurely adding:

```text
commission
settlement
tax calculation
seller billing
buy box
promotion engine
campaign rules
order logic
```

These capabilities may belong to other Bounded Contexts.

---

# 78. Core Rule

Offers owns the canonical answer to:

> What commercial relationship exists between this Seller and this SKU?

It does not answer:

```text
What is the product?        -> Catalog
Who is the Seller?          -> Sellers
How much does it cost?      -> Pricing
Is it available?            -> Availability
Where can it be fulfilled?  -> Fulfillment
How is it delivered?        -> Freight
```

---

# 79. Final Principle

The Offers Domain is the commercial bridge between Catalog and Sellers.

It must remain:

```text
business-focused
catalog-decoupled
seller-decoupled
pricing-independent
availability-independent
fulfillment-independent
freight-independent
database-independent
broker-independent
cloud-independent
AI-provider-independent
```

Catalog may evolve.

Seller integrations may evolve.

Pricing and inventory systems may evolve.

The canonical Offer identity and lifecycle must remain protected.
