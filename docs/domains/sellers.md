# Yunu.Commerce - Sellers Domain

## 1. Purpose

This document defines the Sellers Bounded Context for Yunu.Commerce.

The Sellers Domain owns the canonical representation of parties that sell products through the commerce platform.

Sellers answers the question:

> Who is allowed to sell through Yunu.Commerce?

The Sellers Domain describes seller identity, seller type, lifecycle, commercial participation status and external seller references.

It does not own:

- products
- SKUs
- offers
- prices
- stock
- availability
- freight calculation
- fulfillment inventory
- payment prices

Those concerns belong to other Bounded Contexts.

---

# 2. Domain Responsibility

Sellers is responsible for:

- Seller identity
- Seller lifecycle
- Seller type
- First-party and third-party seller classification
- Seller activation
- Seller suspension
- Seller deactivation
- Seller external references
- Seller commercial identity
- Seller eligibility metadata
- Seller status transitions

The Sellers Domain must remain independent from:

- marketplace provider schemas
- ERP schemas
- database technology
- Kafka
- Redis
- Elasticsearch
- cloud providers
- AI providers

---

# 3. Core Ubiquitous Language

The initial Sellers language includes:

```text
Seller
SellerId
SellerType
SellerStatus
Merchant
FirstPartySeller
ThirdPartySeller
ExternalSellerReference
SellerActivation
SellerSuspension
```

External terminology must be translated into this canonical language before entering the Domain.

---

# 4. Seller

A Seller represents a commercial party authorized to sell items through Yunu.Commerce.

Conceptually:

```text
Seller
│
├── SellerId
├── Name
├── Type
├── Status
└── External References
```

A Seller does not represent a Product or Offer.

The same Product/SKU may be sold by multiple Sellers through different Offers.

---

# 5. Seller Identity

Every Seller must have a canonical Yunu.Commerce identity:

```text
SellerId
```

External identifiers must not replace SellerId.

Examples of external identifiers:

```text
Marketplace Seller Id
ERP Vendor Id
Merchant Id
Legacy Seller Id
Partner Id
```

These should be represented as external references.

---

# 6. Seller Type

Yunu.Commerce must explicitly distinguish commercial ownership.

Initial Seller types:

```text
FirstParty
ThirdParty
```

This distinction must not be inferred indirectly from arbitrary fields.

---

# 7. First-Party Seller - 1P

A First-Party Seller represents inventory commercially sold by the retailer/platform itself.

Conceptually:

```text
SellerType = FirstParty
```

Typical characteristics may include:

```text
platform-owned commercial operation
internal inventory
internal fulfillment capabilities
internal pricing policies
```

However, Sellers does not own those pricing or inventory details.

---

# 8. Third-Party Seller - 3P

A Third-Party Seller represents an external merchant selling through the platform.

Conceptually:

```text
SellerType = ThirdParty
```

Examples include:

```text
Marketplace merchant
Partner retailer
External distributor
Brand-operated store
```

The Seller Domain owns the identity and participation status.

Offers owns what the Seller sells.

---

# 9. 1P / 3P Principle

The architecture must not model 1P and 3P as completely unrelated product models.

Preferred conceptual model:

```text
Catalog

Product
   │
   └── SKU
        │
        ▼
Offers

        ├── Offer
        │    Seller = 1P
        │
        ├── Offer
        │    Seller = 3P A
        │
        └── Offer
             Seller = 3P B
```

The canonical Product/SKU remains independent from the Seller.

---

# 10. Seller Status

Seller lifecycle must be explicit.

Potential initial statuses:

```text
Draft
PendingApproval
Active
Suspended
Inactive
Archived
```

Exact transitions will be refined from business requirements.

---

# 11. Draft Seller

A Draft Seller exists in the platform but has not completed the required onboarding or validation process.

A Draft Seller must not automatically be considered commercially active.

---

# 12. Pending Approval

A Seller may enter:

```text
PendingApproval
```

when onboarding data exists but requires approval.

Potential validation may include:

```text
commercial data
legal information
integration readiness
required configuration
```

The exact approval rules are outside the initial skeleton and must not be invented during scaffolding.

---

# 13. Active Seller

An Active Seller is eligible to participate in commerce flows.

Conceptually:

```text
Seller.Status = Active
```

Active status does not imply:

```text
every Offer is active
every SKU has stock
every price is valid
every region is available
```

Those decisions belong to their respective contexts.

---

# 14. Suspended Seller

A Suspended Seller remains known to the platform but is temporarily prevented from participating in new commercial activity.

Suspension must be modeled explicitly rather than deleting the Seller.

Potential reasons may eventually include:

```text
commercial policy
integration problems
compliance
operational issues
manual administration
```

Exact reasons will be modeled only when required.

---

# 15. Inactive Seller

An Inactive Seller is not currently participating in commerce.

Inactive is conceptually different from Suspended.

A future business decision must define whether reactivation is allowed and under which conditions.

---

# 16. Archived Seller

Archived represents a historical Seller that should no longer participate in normal operations.

Archival should preserve historical references required by:

```text
orders
offers
audit
analytics
integration history
```

Physical deletion should not be the default lifecycle mechanism.

---

# 17. Seller State Transitions

Potential conceptual transitions:

```text
Draft
  │
  ▼
PendingApproval
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
  │
  └────────► Archived
```

This diagram is an architectural candidate.

Exact legal transitions must be explicitly approved before Domain implementation.

---

# 18. Merchant

The term Merchant may appear in external marketplace systems.

Within Yunu.Commerce, Seller is the preferred canonical business term unless a distinct Merchant concept emerges from real requirements.

Do not create duplicate concepts merely because external systems use different terminology.

---

# 19. Seller Name

Seller should have a meaningful business display identity.

Potential concepts include:

```text
LegalName
DisplayName
TradingName
```

These should be introduced only when required by business use cases.

Do not overload a single arbitrary Name property if different meanings become important.

---

# 20. External Seller References

A Seller may map to identifiers in multiple systems.

Conceptually:

```text
ExternalSellerReference

System
Type
Value
```

Examples:

```text
System = MarketplaceA
Type = MerchantId
Value = 12345
```

```text
System = ERP
Type = VendorId
Value = 90876
```

External references must not control internal identity.

---

# 21. External Reference Uniqueness

Where required, a combination such as:

```text
System + Type + Value
```

should uniquely identify the external Seller mapping.

Exact uniqueness rules must be defined during implementation.

---

# 22. Catalog Boundary

Sellers does not own:

```text
Product
SKU
Category
Brand
Product attributes
Product specifications
Product media
```

Those concepts belong to Catalog.

A Seller participates commercially through Offers referencing canonical Catalog identities.

---

# 23. Offers Boundary

Offers connects:

```text
Seller
+
SKU
```

into a commercial proposition.

Conceptually:

```text
Offer

OfferId
SellerId
SkuId
Status
```

Sellers owns SellerId and Seller lifecycle.

Offers owns the commercial relationship.

---

# 24. Seller Does Not Own Offers

The Seller Aggregate must not contain an ever-growing collection of all Offers.

Avoid:

```text
Seller
└── Millions of Offers
```

Offers belong to the Offers Bounded Context and may reference SellerId.

This prevents an enormous Aggregate and unnecessary cross-context coupling.

---

# 25. Pricing Boundary

Sellers does not own:

```text
Regular Price
Sale Price
PIX Price
Boleto Price
Installments
Regional Price
Promotional Price
```

Pricing owns price behavior.

Prices may ultimately be associated with an Offer that references a Seller.

---

# 26. Availability Boundary

Sellers does not own:

```text
Stock
National Availability
Regional Availability
Branch Availability
Reservation
```

Availability owns sellability/stock state.

Seller identity may participate in availability keys where required.

---

# 27. Fulfillment Boundary

A Seller is not automatically a Fulfillment Node.

These are different concepts.

Example:

```text
Seller A
   │
   ├── fulfilled from Warehouse X
   └── fulfilled from Store Y
```

Fulfillment owns physical or logical fulfillment nodes.

Sellers owns the commercial party.

---

# 28. Freight Boundary

Sellers does not calculate freight.

Freight may use Seller, Offer or Fulfillment information as input, but calculation belongs to Freight.

---

# 29. Search Boundary

Seller information may be projected into Elasticsearch for customer-facing discovery.

Example:

```text
SellerUpdated
      │
      ▼
Search Projection
      │
      ▼
Elasticsearch
```

Elasticsearch is not authoritative Seller storage.

---

# 30. AI Boundary

AI may assist Seller-related workflows in the future.

Potential examples:

```text
Seller data normalization
Classification assistance
Integration mapping suggestions
Seller support automation
```

AI must not autonomously bypass Seller Domain invariants.

Provider-specific AI logic belongs outside Sellers Domain.

---

# 31. Aggregate Root Candidate

The primary Aggregate Root candidate is:

```text
Seller
```

Conceptually:

```text
Seller
│
├── SellerId
├── SellerType
├── SellerStatus
├── Business Identity
└── External References
```

Exact properties and behaviors will be introduced incrementally.

---

# 32. Value Object Candidates

Potential Value Objects include:

```text
SellerId
SellerName
ExternalSellerReference
TaxIdentifier
```

A Value Object should exist only when meaningful semantics or invariants justify it.

---

# 33. Tax and Legal Information

Seller onboarding may eventually require:

```text
Tax Identifier
Legal Name
Address
Legal Entity Type
Country
```

These concepts must be modeled carefully when real onboarding requirements are defined.

Sensitive or regulated data must not be added casually to events or logs.

---

# 34. Domain Service Candidates

Domain Services should only exist when business behavior cannot naturally belong to Seller.

Avoid generic containers such as:

```text
SellerService
MerchantService
```

without a specific domain responsibility.

---

# 35. Repository Boundary

Potential repository contract:

```text
ISellerRepository
```

The repository contract belongs to an inner layer.

It must not expose:

```text
DbContext
MongoCollection
SQL Connection
Redis
Elasticsearch
```

Infrastructure implements the repository port.

---

# 36. Persistence Independence

Sellers Domain must remain independent from the persistence technology.

Possible future persistence:

```text
SQL Server
PostgreSQL
MongoDB
```

The choice must not affect Domain behavior.

---

# 37. Relational Persistence Candidate

Seller data has characteristics that may fit relational persistence well:

```text
identity
status
external references
legal/commercial relationships
uniqueness constraints
```

A relational database is therefore a strong candidate for Sellers persistence.

This is a data architecture decision and must be recorded separately before implementation.

---

# 38. Cache Independence

Redis may later cache Seller information.

Redis must not become the authoritative Seller source.

The Domain must not reference Redis.

---

# 39. Domain Events

Potential Seller Domain Events include:

```text
SellerCreatedDomainEvent
SellerSubmittedForApprovalDomainEvent
SellerActivatedDomainEvent
SellerSuspendedDomainEvent
SellerReactivatedDomainEvent
SellerDeactivatedDomainEvent
SellerArchivedDomainEvent
```

Exact events must emerge from implemented business behavior.

---

# 40. Integration Events

Potential Integration Events include:

```text
SellerCreated
SellerActivated
SellerSuspended
SellerReactivated
SellerDeactivated
SellerArchived
SellerUpdated
```

Integration Events are external contracts and must remain distinct from Domain Events.

---

# 41. Seller Suspension Event

A Seller suspension may affect multiple contexts.

Conceptually:

```text
SellerSuspended
      │
      ▼
Kafka
      │
      ├── Offers reacts
      ├── Search reacts
      └── other interested contexts react
```

Sellers must not directly update another context's database.

---

# 42. Eventual Consistency

When Seller status changes:

```text
Seller = Suspended
```

other contexts may react asynchronously.

For a short period:

```text
Sellers = new state
Search projection = previous state
```

This is expected in the Event-Driven Architecture.

Critical synchronous validations may use explicit Application boundaries where eventual consistency is insufficient.

---

# 43. Seller Activation Use Case

Potential conceptual flow:

```text
ActivateSellerCommand
       │
       ▼
Sellers Application
       │
       ▼
Seller Aggregate
       │
       ▼
Validate transition
       │
       ▼
Active
       │
       ▼
SellerActivatedDomainEvent
```

Infrastructure concerns remain outside the Aggregate.

---

# 44. Seller Suspension Use Case

Conceptually:

```text
SuspendSellerCommand
       │
       ▼
Seller Aggregate
       │
       ▼
Validate transition
       │
       ▼
Suspended
       │
       ▼
SellerSuspendedDomainEvent
       │
       ▼
Integration Event
```

---

# 45. Application Use Cases

Potential future Sellers use cases include:

```text
CreateSeller
UpdateSeller
SubmitSellerForApproval
ActivateSeller
SuspendSeller
ReactivateSeller
DeactivateSeller
ArchiveSeller
GetSellerById
GetSellerByExternalReference
```

Use cases belong to Application.

State transition rules belong to Domain.

---

# 46. CQRS

Sellers Application may use CQRS.

Commands:

```text
CreateSellerCommand
ActivateSellerCommand
SuspendSellerCommand
```

Queries:

```text
GetSellerByIdQuery
GetSellerByExternalReferenceQuery
ListActiveSellersQuery
```

CQRS does not imply separate databases.

---

# 47. Seller Lookup

Other contexts may need to verify Seller identity or status.

Possible strategies include:

```text
local projection
integration event
Application API
cached reference data
```

The appropriate strategy depends on consistency requirements.

Direct access to the Sellers database is forbidden.

---

# 48. Cross-Context References

Other contexts should reference Seller using:

```text
SellerId
```

rather than importing the Seller Aggregate.

Example:

```text
Offer
├── OfferId
├── SellerId
└── SkuId
```

This preserves Aggregate and Bounded Context independence.

---

# 49. No Cross-Context Domain Dependency

Forbidden:

```text
Offers.Domain
    →
Sellers.Domain.Seller
```

Preferred:

```text
Offers.Domain
    →
SellerId value/reference meaningful inside Offers
```

or contracts at an appropriate integration boundary.

---

# 50. Anti-Corruption Layer

External marketplace Seller structures must pass through an Anti-Corruption Layer.

Conceptually:

```text
Marketplace Merchant
       │
       ▼
Integration Adapter
       │
       ▼
Anti-Corruption Mapping
       │
       ▼
Canonical Seller Input
       │
       ▼
Sellers Application
```

External models must not become Domain Entities.

---

# 51. Seller Import

Bulk Seller import is an Application/Integration concern.

The Domain must not parse:

```text
CSV
Excel
external JSON
marketplace payload
ERP records
```

Adapters translate external formats into canonical inputs.

---

# 52. Idempotent Integration

External Seller synchronization must support idempotency.

Repeated processing of the same external Seller update must not create duplicate canonical Sellers.

External reference uniqueness may assist this behavior.

---

# 53. Concurrency

Seller lifecycle changes may require optimistic concurrency.

Potential strategies:

```text
Version
ETag
Persistence-level optimistic concurrency
```

The Domain must not depend on the chosen database implementation.

---

# 54. Auditability

Seller changes may require:

```text
CreatedAtUtc
UpdatedAtUtc
CreatedBy
UpdatedBy
```

Lifecycle transitions may also require explicit reason metadata.

Audit implementation must remain infrastructure-independent.

---

# 55. Suspension Reason

A future Seller suspension model may require:

```text
ReasonCode
Description
SuspendedAtUtc
SuspendedBy
```

These should only be introduced when business requirements demand them.

---

# 56. Security

Seller management operations may require stronger authorization than public catalog reads.

Examples:

```text
Seller administrator
Marketplace operator
Internal commerce operator
Integration identity
```

Authorization belongs to Application/Host boundaries.

Domain rules must not depend on ASP.NET authorization APIs.

---

# 57. Sensitive Data

Seller data may eventually include business-sensitive or regulated information.

Such data must not be indiscriminately:

```text
logged
published to Kafka
indexed in Elasticsearch
included in AI prompts
cached without policy
```

Only necessary information should cross boundaries.

---

# 58. Error Semantics

Potential meaningful errors include:

```text
SellerNotFound
SellerAlreadyActive
SellerAlreadySuspended
InvalidSellerState
DuplicateExternalSellerReference
InvalidSellerType
SellerCannotBeActivated
```

Infrastructure exceptions must not leak directly into Domain/API semantics.

---

# 59. Validation Layers

Application validation may verify:

```text
required fields
input format
command shape
```

Domain validation protects:

```text
seller invariants
valid lifecycle transitions
identity rules
```

Infrastructure validation protects:

```text
database constraints
provider-specific requirements
serialization
```

---

# 60. Testing Strategy

Seller Domain tests should focus on:

```text
creation
activation
suspension
reactivation
deactivation
invalid transitions
identity rules
external reference rules
domain events
```

Pure Aggregate tests should not require infrastructure mocks.

---

# 61. Architecture Questions Before Implementation

Before implementing Seller, explicitly decide:

```text
Can multiple First-Party Sellers exist?

What exactly distinguishes 1P from 3P?

Can SellerType change after creation?

What information is mandatory before activation?

Does Seller require approval?

Who can suspend a Seller?

Can a suspended Seller be reactivated?

What happens to active Offers after suspension?

What Seller data must be projected into other contexts?

What external reference uniqueness is required?

Which legal/tax information belongs here?

What information must never appear in integration events?
```

These decisions must come from business requirements.

---

# 62. Initial Implementation Scope

The first Sellers implementation should remain small.

Recommended initial slice:

```text
Seller
SellerId
SellerType
SellerStatus
basic creation
activation
suspension
Domain Events
repository port
unit tests
```

Do not implement the entire marketplace onboarding ecosystem in the first iteration.

---

# 63. Relationship with Catalog

Catalog answers:

> What is the product?

Sellers answers:

> Who is the commercial seller?

The contexts remain independent.

---

# 64. Relationship with Offers

Offers answers:

> What does this Seller offer for this SKU?

Conceptually:

```text
SellerId
   +
SkuId
   │
   ▼
Offer
```

This is the principal bridge between Sellers and Catalog commerce.

---

# 65. Relationship with Pricing

Pricing answers:

> What price and payment conditions apply to the commercial Offer?

Seller itself does not contain price.

---

# 66. Relationship with Availability

Availability answers:

> Can the item represented by this commercial relationship be sold or fulfilled in the requested scope?

Seller itself does not contain stock.

---

# 67. Relationship with Fulfillment

Fulfillment answers:

> From which physical/logical nodes can the Seller's commercial item be fulfilled?

Seller and FulfillmentNode remain different concepts.

---

# 68. Relationship with Freight

Freight answers:

> How can the item be delivered from an eligible fulfillment source to the destination?

Seller identity may influence freight policies, but Sellers does not calculate freight.

---

# 69. Data Ownership

Sellers is authoritative for:

```text
Seller identity
Seller type
Seller lifecycle
Seller status
Seller external references
```

Other contexts must not directly modify Seller persistence.

---

# 70. Domain Purity

Sellers Domain must not reference:

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

# 71. Evolution Principle

The Sellers model will evolve with real marketplace and 1P/3P use cases.

Avoid prematurely modeling:

```text
commissions
settlement
billing
tax engines
marketplace reputation
seller scoring
contracts
financial accounts
```

unless those capabilities become explicit requirements.

They may eventually belong to additional Bounded Contexts.

---

# 72. Core Rule

Sellers owns the canonical answer to:

> Who is selling?

It does not answer:

```text
What is being sold?       -> Catalog
What is the offer?        -> Offers
How much does it cost?    -> Pricing
Is it available?          -> Availability
Where is it fulfilled?    -> Fulfillment
How is it delivered?      -> Freight
```

---

# 73. Final Principle

The Sellers Domain must preserve a stable canonical Seller identity independently from marketplace integrations and commercial execution details.

It must remain:

```text
business-focused
catalog-independent
offer-independent
pricing-independent
availability-independent
database-independent
broker-independent
cloud-independent
AI-provider-independent
```

External marketplaces may change.

Persistence may change.

Deployment topology may change.

The Seller business identity and lifecycle remain protected.
