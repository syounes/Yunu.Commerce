# Yunu.Commerce - Pricing Domain

## 1. Purpose

This document defines the Pricing Bounded Context for Yunu.Commerce.

The Pricing Domain owns the canonical representation of commercial prices and payment-condition pricing.

Pricing answers the question:

> How much does this commercial Offer cost, under which conditions and in which commercial scope?

Pricing must support commerce scenarios including:

- National price
- Regional price
- Regular price
- Sale price
- PIX price
- Boleto price
- Credit card price
- Installments
- Promotional pricing
- Price validity periods
- Seller-specific pricing
- Offer-specific pricing

Pricing does not own:

- Product descriptive information
- SKU structure
- Seller lifecycle
- Offer lifecycle
- Stock quantity
- Availability
- Fulfillment topology
- Freight calculation
- Search indexes
- AI provider implementation

Those concerns belong to other Bounded Contexts.

---

# 2. Domain Responsibility

Pricing is responsible for:

- Price identity
- Monetary value
- Currency
- Price lifecycle
- Price validity
- National pricing
- Regional pricing
- Payment-method pricing
- PIX pricing
- Boleto pricing
- Credit card conditions
- Installment conditions
- Promotional prices
- Price priority
- Price selection rules
- Commercial price consistency
- Price publication facts

Pricing must remain independent from:

- database technology
- Kafka
- Redis
- Elasticsearch
- Azure
- Google Cloud
- external ERP schemas
- marketplace pricing schemas

---

# 3. Core Ubiquitous Language

The initial Pricing language includes:

```text
Price
PriceId
Money
Currency
RegularPrice
SalePrice
PaymentPrice
PixPrice
BoletoPrice
CreditCardPrice
Installment
PriceScope
NationalPrice
RegionalPrice
Region
PriceValidity
PriceStatus
Promotion
```

The exact model will evolve from business use cases.

---

# 4. Price

A Price represents a monetary commercial condition associated with an Offer or another explicitly defined commercial key.

Conceptually:

```text
Price
│
├── PriceId
├── OfferId
├── Amount
├── Currency
├── Scope
├── Payment Condition
├── Validity
└── Status
```

The final Aggregate structure will be decided before implementation.

---

# 5. Money

Money should be represented as a domain concept rather than an arbitrary floating-point value.

Conceptually:

```text
Money

Amount
Currency
```

Examples:

```text
BRL 4,999.90
USD 999.00
```

Binary floating-point types must not be used for financial monetary calculations.

---

# 6. Currency

Currency must be explicit.

Initial commerce may primarily use:

```text
BRL
```

but the model should not hardcode assumptions that make additional currencies impossible.

Currency representation should follow a recognized standard such as ISO 4217 where appropriate.

---

# 7. Pricing Key

The initial preferred commercial relationship is:

```text
OfferId
   │
   ▼
Pricing
```

An Offer already represents:

```text
SellerId + SkuId + commercial relationship
```

Therefore Pricing should normally price the Offer rather than duplicate Catalog and Seller ownership.

The final key strategy must be validated against business requirements.

---

# 8. National Price

A National Price applies to the national commercial scope when no more specific regional rule overrides it.

Conceptually:

```text
Offer
  │
  └── National Price
```

Example:

```text
Offer 123

National Regular Price = R$ 5,299.00
National Sale Price    = R$ 4,999.00
```

National does not mean every customer can buy the item.

Availability and freight remain independent.

---

# 9. Regional Price

A Regional Price applies to a defined geographic scope.

Conceptually:

```text
Offer
  │
  ├── National Price
  │
  ├── Southeast Price
  │
  ├── South Price
  │
  └── Northeast Price
```

A regional price may override the national price according to explicit pricing rules.

---

# 10. Region

Pricing must not use arbitrary region strings when canonical regional identity is required.

Conceptually:

```text
RegionId
```

The exact ownership of geographic reference data will be defined separately.

Pricing may maintain the identifiers required for its own rules without importing another Domain Aggregate.

---

# 11. Regional Resolution

A conceptual resolution strategy may be:

```text
Request Commercial Context
        │
        ▼
Specific Regional Price exists?
        │
       Yes
        │
        ▼
Use Regional Price
        │
       No
        │
        ▼
Use National Price
```

This is a candidate rule and must be explicitly validated before implementation.

---

# 12. Regular Price

Regular Price represents the standard commercial price before applicable sale or promotional conditions.

Example:

```text
Regular Price = R$ 5,299.00
```

Regular Price semantics must be explicit and must not simply be inferred from whichever value is highest.

---

# 13. Sale Price

Sale Price represents a currently applicable selling price different from the regular reference price.

Example:

```text
Regular Price = R$ 5,299.00
Sale Price    = R$ 4,999.00
```

Rules governing whether Sale Price can exceed Regular Price must be defined explicitly.

---

# 14. Payment Method Pricing

Different payment methods may have different prices.

Examples:

```text
PIX
Boleto
Credit Card
```

Conceptually:

```text
Base Commercial Price
        │
        ├── PIX Price
        ├── Boleto Price
        └── Credit Card Conditions
```

The model must avoid duplicating unrelated pricing state unnecessarily.

---

# 15. PIX Price

PIX may have a specific payment price.

Example:

```text
Regular Sale Price = R$ 4,999.00
PIX Price          = R$ 4,749.05
```

The Domain should model the resulting commercial condition, not embed payment-provider SDK details.

---

# 16. Boleto Price

Boleto may have a specific price.

Example:

```text
Sale Price   = R$ 4,999.00
Boleto Price = R$ 4,899.00
```

Whether Boleto has its own price or shares a cash-payment policy must be decided from business requirements.

---

# 17. Credit Card Price

Credit card pricing may depend on installment conditions.

Example:

```text
1x R$ 4,999.00
5x R$ 999.80
10x R$ 499.90
```

Future rules may include interest-bearing installments.

The Domain must distinguish:

```text
total price
installment amount
installment count
interest conditions
```

---

# 18. Installment

Installment is a pricing concept describing payment division.

Potential representation:

```text
InstallmentCondition

NumberOfInstallments
InstallmentAmount
TotalAmount
InterestRate
InterestFree
```

Exact structure will be defined from payment requirements.

---

# 19. Installment Invariants

Potential invariants may include:

```text
NumberOfInstallments > 0
InstallmentAmount >= 0
TotalAmount >= 0
Currency must match
```

Financial rounding rules must be explicit.

These rules must be finalized before implementation.

---

# 20. Price Scope

Price Scope describes where a price applies.

Potential scopes include:

```text
National
Regional
```

Future scopes may include:

```text
Channel
CustomerSegment
B2B Contract
Campaign
```

Do not add them before actual use cases require them.

---

# 21. Price Validity

Prices may have temporal validity.

Conceptually:

```text
ValidFromUtc
ValidUntilUtc
```

Example:

```text
Price valid from:
2026-08-11T00:00:00Z

until:
2026-08-20T23:59:59Z
```

Open-ended validity may be allowed depending on business rules.

---

# 22. Time Semantics

Persisted and integration timestamps should use UTC.

Regional business dates must be converted explicitly at system boundaries.

Pricing must avoid ambiguous local-time behavior.

The Domain should model business time semantics independently from database serialization.

---

# 23. Price Status

Potential initial statuses:

```text
Draft
Scheduled
Active
Expired
Inactive
Canceled
```

Exact statuses and transitions must be validated from real pricing workflows.

---

# 24. Scheduled Price

A price with a future validity start may be considered:

```text
Scheduled
```

Conceptually:

```text
Now < ValidFrom
```

Whether Scheduled is persisted or derived should be decided during modeling.

---

# 25. Active Price

An Active Price is currently applicable according to its lifecycle and validity.

Conceptually:

```text
ValidFrom <= Now
AND
(ValidUntil is null OR ValidUntil >= Now)
```

Additional commercial conditions may apply.

---

# 26. Expired Price

A price becomes expired when its validity window ends.

Conceptually:

```text
ValidUntil < Now
```

Whether expiration is represented by stored state or derived from time must be explicitly decided.

---

# 27. Price Priority

Multiple candidate prices may exist.

The Domain must define deterministic selection rules.

A future conceptual priority might include:

```text
more specific scope
    >
less specific scope
```

For example:

```text
Regional
    >
National
```

Payment-specific prices may then apply according to payment method.

Exact precedence must be documented and tested.

---

# 28. Price Resolution

Conceptually:

```text
Offer
+
Region
+
Payment Method
+
Date/Time
      │
      ▼
Pricing Resolution
      │
      ▼
Applicable Price
```

The resolution algorithm must be deterministic.

---

# 29. Price Resolution Is Domain Behavior

Selecting the applicable commercial price is business behavior.

It must not be hidden inside:

```text
SQL query
Mongo query
Redis key logic
API controller
Elasticsearch script
```

Persistence may optimize candidate retrieval, but business precedence belongs to the Pricing model.

---

# 30. Price Overlap

Pricing may need rules preventing or resolving overlapping validity periods.

Example:

```text
Price A
Region = South
Valid = Aug 1 - Aug 20

Price B
Region = South
Valid = Aug 10 - Aug 30
```

Whether overlap is:

```text
forbidden
allowed with priority
allowed by price type
```

must be explicitly defined.

---

# 31. Promotional Pricing

Promotional pricing may introduce temporary commercial prices.

Potential concepts:

```text
PromotionId
CampaignId
PromotionalPrice
ValidFrom
ValidUntil
Priority
```

However, a full Promotion/Campaign engine may become a separate Bounded Context.

Pricing should not absorb an entire marketing platform prematurely.

---

# 32. Campaign Boundary

If future commerce introduces complex campaign behavior such as:

```text
coupon rules
customer segmentation
buy-one-get-one
progressive discounts
campaign eligibility
marketing budgets
```

those capabilities should likely belong to a dedicated Promotions/Campaigns context.

Pricing would consume the resulting applicable commercial condition.

---

# 33. Catalog Boundary

Pricing does not own:

```text
Product
SKU descriptive data
Category
Brand
Attributes
Specifications
Media
```

Catalog owns those concepts.

Pricing should reference commercial identifiers rather than duplicate Catalog data.

---

# 34. Sellers Boundary

Pricing does not own Seller lifecycle.

Seller identity participates indirectly through Offer.

Conceptually:

```text
Seller
  │
  ▼
Offer
  │
  ▼
Pricing
```

---

# 35. Offers Boundary

Offers owns the commercial Seller-SKU relationship.

Pricing owns monetary conditions associated with that relationship.

Conceptually:

```text
Offer
  │
  ▼
Price
```

Pricing must not change Offer lifecycle directly.

---

# 36. Availability Boundary

Pricing does not own:

```text
Stock
Availability
Regional stock
Branch stock
Reservation
```

A valid Price does not imply that an Offer is available.

---

# 37. Fulfillment Boundary

Pricing does not own:

```text
Warehouse
Store
Branch
Distribution Center
Fulfillment Node
```

Fulfillment concerns remain independent.

---

# 38. Freight Boundary

Product price and freight price are different commercial concepts.

Pricing owns product/Offer pricing.

Freight owns delivery pricing.

Avoid storing freight calculation inside the Product Price Aggregate.

---

# 39. Search Boundary

Search may project the currently applicable price into Elasticsearch.

Conceptually:

```text
PriceChanged
     │
     ▼
Search Consumer
     │
     ▼
Product Search Document
```

Elasticsearch is a projection.

It is not the authoritative Pricing database.

---

# 40. Redis Boundary

Redis may be valuable for high-volume price reads.

Conceptually:

```text
Pricing Source of Truth
        │
        ▼
Price Cache
        │
        ▼
Redis
```

Redis must not become the sole canonical source of Price state.

Cache invalidation/update strategy must be explicit.

---

# 41. AI Boundary

AI may assist pricing analysis in the future.

Potential examples:

```text
price anomaly detection
competitive pricing suggestions
price optimization recommendations
forecasting
```

AI-generated recommendations must not bypass Pricing Domain rules.

AI provider-specific code belongs outside Pricing Domain.

---

# 42. Aggregate Root Candidates

Potential Aggregate Roots include:

```text
Price
PriceBook
```

The final choice depends on:

```text
transaction boundaries
price volume
concurrency
regional cardinality
bulk updates
validity rules
```

A giant Offer Price aggregate containing all regions and all future prices should be avoided unless justified.

---

# 43. Price Aggregate Candidate

A simple initial model may be:

```text
Price
│
├── PriceId
├── OfferId
├── Scope
├── Money
├── Validity
├── Status
└── Payment Conditions
```

This is a modeling candidate, not a final implementation contract.

---

# 44. PriceBook Candidate

A future PriceBook concept may group prices by commercial purpose.

Examples:

```text
Retail Price Book
Marketplace Price Book
B2B Price Book
```

Do not introduce PriceBook until real requirements justify it.

---

# 45. Value Object Candidates

Potential Value Objects include:

```text
PriceId
Money
Currency
PriceValidity
RegionId
PaymentMethod
InstallmentCondition
Percentage
```

Value Objects should enforce meaningful financial semantics.

---

# 46. Decimal Precision

Financial values must use decimal arithmetic with explicit precision and rounding.

Never use binary floating-point values such as:

```text
float
double
```

for canonical monetary calculations.

---

# 47. Rounding

Rounding rules must be explicit.

Potential concerns include:

```text
installment division
percentage discounts
tax calculations
interest calculations
currency minor units
```

Do not rely implicitly on database or programming-language defaults.

---

# 48. Negative Prices

Whether negative prices are legal must be explicit.

For ordinary product selling prices, a likely invariant is:

```text
Amount >= 0
```

The exact rule must be approved before implementation.

---

# 49. Zero Price

Zero price may represent:

```text
free product
promotion
invalid commercial state
```

depending on the business.

Do not assume zero is automatically valid or invalid.

---

# 50. Repository Boundary

Potential repository contracts include:

```text
IPriceRepository
```

or more specialized ports if justified.

Repository contracts must not expose:

```text
DbContext
MongoCollection
SQL Connection
Redis client
Elasticsearch client
```

---

# 51. Persistence Independence

Pricing Domain must remain independent from persistence technology.

Possible persistence options include:

```text
SQL Server
PostgreSQL
MongoDB
```

Data Architecture will decide the initial store.

---

# 52. Relational Persistence Candidate

Pricing has characteristics that often fit relational storage:

```text
financial precision
validity ranges
uniqueness
transactional updates
auditing
constraints
```

A relational database is therefore a strong candidate.

This must be recorded as a separate architecture decision.

---

# 53. High Read Volume

Commerce pricing is typically read much more frequently than it is written.

Architecture may therefore use:

```text
canonical relational store
+
Redis
+
Elasticsearch projection
```

for different access patterns.

Each storage technology has a distinct role.

---

# 54. CQRS

Pricing is a strong candidate for CQRS.

Write side:

```text
CreatePrice
ChangePrice
SchedulePrice
CancelPrice
```

Read side:

```text
GetCurrentPrice
GetPriceByRegion
GetPaymentPrices
ResolveCommercialPrice
```

CQRS does not require separate databases.

---

# 55. Application Use Cases

Potential future use cases include:

```text
CreateNationalPrice
CreateRegionalPrice
UpdatePrice
SchedulePrice
ActivatePrice
CancelPrice
GetCurrentPrice
GetRegionalPrice
GetPaymentPrices
GetInstallmentOptions
ResolvePrice
```

Bulk use cases may include:

```text
ImportPrices
SynchronizePrices
BulkPriceUpdate
```

---

# 56. Price Creation Flow

Conceptually:

```text
CreatePriceCommand
       │
       ▼
Pricing Application
       │
       ▼
Pricing Domain
       │
       ▼
Validate invariants
       │
       ▼
Repository Port
       │
       ▼
Persistence Adapter
```

---

# 57. Price Resolution Flow

Conceptually:

```text
OfferId
Region
PaymentMethod
Current Time
       │
       ▼
Pricing Application
       │
       ▼
Retrieve candidates
       │
       ▼
Domain resolution policy
       │
       ▼
Applicable Price
```

---

# 58. Domain Events

Potential Pricing Domain Events include:

```text
PriceCreatedDomainEvent
PriceChangedDomainEvent
PriceActivatedDomainEvent
PriceExpiredDomainEvent
PriceCanceledDomainEvent
RegionalPriceCreatedDomainEvent
PaymentPriceChangedDomainEvent
```

Exact events must emerge from real Domain behavior.

---

# 59. Integration Events

Potential Integration Events include:

```text
PriceCreated
PriceChanged
PriceActivated
PriceExpired
PriceCanceled
CurrentPriceChanged
```

Consumers may include:

```text
Search
Cache projection
Analytics
External integrations
```

---

# 60. Current Price Changed

A particularly useful integration fact may be:

```text
CurrentPriceChanged
```

rather than forcing consumers to reconstruct every internal Pricing transition.

The event contract should expose only what consumers legitimately need.

---

# 61. Transactional Outbox

Price changes that generate Integration Events should use the Transactional Outbox pattern where appropriate.

Conceptually:

```text
Price transaction
      │
      ├── Persist Price
      └── Persist Outbox Message
               │
               ▼
          Outbox Worker
               │
               ▼
             Kafka
```

---

# 62. Inbox Pattern

Consumers of Pricing events should use idempotent processing where required.

Repeated event delivery must not corrupt projections.

---

# 63. Idempotent Price Imports

External price synchronization may send duplicate messages or records.

Pricing Application must support idempotent integration strategies.

Potential mechanisms include:

```text
ExternalPriceReference
IdempotencyKey
MessageId
SourceVersion
```

Exact implementation belongs to Infrastructure/Application.

---

# 64. External Price Reference

External systems may identify prices differently.

Conceptually:

```text
ExternalPriceReference

System
Type
Value
```

External identifiers must not replace canonical Pricing identity.

---

# 65. Anti-Corruption Layer

External pricing models must be translated.

Conceptually:

```text
ERP Price
Marketplace Price
Legacy Price
      │
      ▼
Integration Adapter
      │
      ▼
Anti-Corruption Layer
      │
      ▼
Canonical Pricing Input
      │
      ▼
Pricing Application
```

External DTOs must not become Domain models.

---

# 66. Bulk Pricing

Large commerce platforms may process very large price updates.

Examples:

```text
national repricing
regional repricing
marketplace synchronization
campaign activation
scheduled price changes
```

Bulk processing must preserve business invariants while avoiding unnecessary Aggregate loading.

---

# 67. Concurrency

Pricing requires careful concurrency control.

Examples:

```text
two price updates for the same Offer
campaign activation while manual update occurs
regional update concurrent with national update
```

Potential strategies include:

```text
optimistic concurrency
versioning
database constraints
message ordering
partitioning
```

Domain semantics must remain independent from technical implementation.

---

# 68. Kafka Partitioning Consideration

Pricing events may require ordering by a stable business key.

Potential partition keys include:

```text
OfferId
PriceId
```

The final strategy belongs to Integration Architecture.

The Domain must not know Kafka partitions.

---

# 69. Auditability

Financial changes should be highly auditable.

Potential metadata includes:

```text
CreatedAtUtc
UpdatedAtUtc
CreatedBy
UpdatedBy
Source
CorrelationId
Reason
```

Historical price changes may require a dedicated audit/history strategy.

---

# 70. Price History

Current Price and Price History are different access patterns.

The architecture may preserve historical versions for:

```text
audit
customer support
analytics
regulatory requirements
commercial investigation
```

History must not unnecessarily inflate the active Aggregate.

---

# 71. Security

Price mutation is a sensitive operation.

Authorization may distinguish:

```text
Pricing operator
Seller integration
Internal commerce system
Administrative user
Automated repricing service
```

Authorization belongs to Application/Host boundaries.

Domain invariants remain independent from authentication technology.

---

# 72. Error Semantics

Potential meaningful errors include:

```text
PriceNotFound
InvalidPriceAmount
InvalidCurrency
InvalidPriceValidity
OverlappingPrice
InvalidPriceState
PriceAlreadyActive
PriceAlreadyExpired
InvalidInstallmentCondition
NoApplicablePrice
```

Infrastructure exceptions must not leak directly into API semantics.

---

# 73. Validation Layers

Application validation may verify:

```text
required input
format
command shape
```

Domain validation protects:

```text
financial invariants
validity
state transitions
price precedence
payment conditions
```

Infrastructure validation protects:

```text
database constraints
serialization
external provider requirements
```

---

# 74. Testing Strategy

Pricing Domain tests must heavily cover:

```text
money arithmetic
currency rules
regional precedence
national fallback
payment-method pricing
validity periods
overlap rules
installments
rounding
state transitions
price resolution
Domain Events
```

Pricing logic must be deterministic and extensively tested.

---

# 75. Architecture Questions Before Implementation

Before implementing Pricing, explicitly decide:

```text
Is Price attached primarily to OfferId?

Can an Offer have multiple simultaneously active prices?

What is the exact national/regional precedence?

Can regional price exist without national price?

What defines a Region?

Can PIX and Boleto prices differ?

Does credit card price differ from sale price?

How are installments calculated?

Are installment prices stored or calculated?

What rounding rules apply?

Are overlapping validity periods allowed?

How are promotions represented?

Should Scheduled/Expired be stored or derived?

How much price history must be retained?

What is the expected read/write volume?

What consistency is required between Pricing, Redis and Search?
```

These decisions must be made before detailed implementation.

---

# 76. Initial Implementation Scope

The first Pricing implementation should remain intentionally small.

Recommended first slice:

```text
Price
PriceId
OfferId
Money
Currency
PriceScope
National Price
Regional Price
PriceValidity
basic price creation
current-price resolution
Domain Events
repository port
unit tests
```

PIX, Boleto, credit card installments and complex promotions should be added incrementally after the base pricing model is stable.

---

# 77. Relationship with Catalog

Catalog answers:

> What is the Product/SKU?

Pricing does not duplicate Product information.

---

# 78. Relationship with Sellers

Sellers answers:

> Who is the Seller?

Pricing normally reaches Seller commercial identity through Offer.

---

# 79. Relationship with Offers

Offers answers:

> What Seller-SKU commercial relationship exists?

Pricing answers:

> How much does that Offer cost?

Conceptually:

```text
OfferId
   │
   ▼
Pricing
```

---

# 80. Relationship with Availability

Pricing answers:

> What is the commercial price?

Availability answers:

> Can it currently be sold/fulfilled?

A valid price does not imply availability.

---

# 81. Relationship with Fulfillment

Pricing does not own fulfillment nodes or stock.

Regional pricing and fulfillment regions may use related geographic identifiers, but their responsibilities remain separate.

---

# 82. Relationship with Freight

Pricing and Freight both deal with monetary values but represent different domains.

```text
Pricing
→ Product/Offer commercial price

Freight
→ Delivery commercial price
```

They must not be collapsed merely because both use Money.

---

# 83. Relationship with Search

Search may expose denormalized fields such as:

```text
regularPrice
salePrice
pixPrice
installmentSummary
```

These are projections.

Pricing remains authoritative.

---

# 84. Customer Price View

A customer-facing price response may eventually combine:

```text
Offer
Region
Regular Price
Sale Price
PIX Price
Boleto Price
Installments
Promotion
```

This is a read model.

It is not necessarily the Pricing Aggregate shape.

---

# 85. Buyability

Pricing is one component of buyability.

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

Pricing must not assume ownership of the complete buyability decision.

---

# 86. Data Ownership

Pricing is authoritative for:

```text
commercial price
regional price
payment price
price validity
price lifecycle
price resolution rules
```

Other contexts must not directly update Pricing persistence.

---

# 87. No Shared Database Ownership

Even if Pricing and another module initially share the same database technology, ownership remains explicit.

Forbidden:

```text
Offers directly updating Pricing tables
```

Forbidden:

```text
Search directly correcting canonical Price data
```

Communication occurs through:

```text
Application boundary
Contracts
Integration Events
Projections
```

---

# 88. Domain Purity

Pricing Domain must not reference:

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

# 89. Evolution Principle

Pricing will evolve incrementally.

Avoid prematurely implementing:

```text
dynamic pricing
competitive pricing
tax engines
coupon engines
loyalty pricing
customer-specific pricing
B2B contract pricing
machine-learning repricing
complex promotion stacking
```

These capabilities should be introduced only when explicit use cases require them.

---

# 90. Core Rule

Pricing owns the canonical answer to:

> How much does this Offer cost under the applicable commercial conditions?

It does not answer:

```text
What is the product?        -> Catalog
Who is selling it?          -> Sellers
What is being offered?      -> Offers
Is it available?            -> Availability
Where is it fulfilled?      -> Fulfillment
How much is delivery?       -> Freight
```

---

# 91. Final Principle

The Pricing Domain protects the monetary and commercial pricing rules of Yunu.Commerce.

It must remain:

```text
financially precise
deterministic
auditable
region-aware
payment-aware
offer-oriented
database-independent
cache-independent
search-independent
broker-independent
cloud-independent
AI-provider-independent
```

Persistence may change.

Cache technology may change.

Search technology may change.

External pricing systems may change.

The Pricing business rules and monetary semantics must remain protected.
