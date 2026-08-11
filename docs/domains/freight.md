# Yunu.Commerce - Freight Domain

## 1. Purpose

This document defines the Freight Bounded Context for Yunu.Commerce.

The Freight Domain owns the business rules required to determine how a commercial item can be delivered from an eligible fulfillment origin to a customer destination.

Freight answers:

> How can this item be delivered, from where, at what freight cost, and within what delivery promise?

The platform must support:

- National and regional freight
- Origin and destination resolution
- Postal-code based delivery
- Region-based delivery
- Carrier integration
- Delivery methods
- Freight quotation
- Freight price
- Delivery SLA
- Delivery estimates
- Multiple fulfillment origins
- Store and distribution-center shipping
- First-party and third-party freight scenarios
- Pickup options where applicable

Freight does not own:

- Product descriptive information
- SKU identity
- Seller lifecycle
- Offer lifecycle
- Product price
- PIX/Boleto/Card prices
- Physical stock quantity
- Canonical Fulfillment Node master data
- Search indexes
- AI provider implementations

---

# 2. Domain Responsibility

Freight is responsible for:

- Freight quotation
- Freight eligibility
- Origin-to-destination delivery evaluation
- Freight price
- Delivery method
- Delivery SLA
- Delivery promise
- Carrier option representation
- Regional freight rules
- Delivery restrictions
- Freight option selection
- Freight business invariants

Freight must remain independent from:

- carrier SDKs
- database technology
- Kafka
- Redis
- Elasticsearch
- Azure
- Google Cloud
- external logistics schemas

---

# 3. Core Ubiquitous Language

The initial Freight language includes:

```text
FreightQuote
FreightQuoteId
Origin
Destination
PostalCode
RegionId
Carrier
CarrierId
DeliveryMethod
FreightPrice
DeliverySla
DeliveryPromise
ShippingOption
FreightRestriction
FulfillmentNodeId
OfferId
SkuId
```

External logistics terminology must be translated into this canonical language.

---

# 4. Freight Quote

A FreightQuote represents a delivery proposition for a commercial item.

Conceptually:

```text
FreightQuote
│
├── FreightQuoteId
├── Origin
├── Destination
├── DeliveryMethod
├── Carrier
├── FreightPrice
└── DeliveryPromise
```

A quote is not the same thing as an Order shipment.

Freight quotation occurs before order fulfillment execution.

---

# 5. Origin

Origin represents the fulfillment source used for delivery evaluation.

Conceptually:

```text
Origin
└── FulfillmentNodeId
```

Fulfillment owns the canonical Fulfillment Node.

Freight consumes the node identity and relevant location/capability information.

---

# 6. Destination

Destination represents the customer delivery destination.

Potential information includes:

```text
Country
State
City
PostalCode
RegionId
GeoLocation
```

The exact minimum information depends on freight-provider requirements.

---

# 7. Postal Code

PostalCode should be treated as a meaningful concept rather than an arbitrary string when validation and routing rules are required.

For Brazil:

```text
CEP
```

may be the principal freight-resolution key.

The Domain should not depend on a specific postal-code provider.

---

# 8. Region

Freight may support regional rules through:

```text
RegionId
```

Examples:

```text
Southeast
South
Northeast
```

However, freight routing may require finer geographic granularity than broad regions.

---

# 9. Regional Freight

Regional Freight represents delivery rules or commercial freight conditions applicable to a geographic scope.

Conceptually:

```text
Origin
+
Destination Region
+
Commercial Item
      │
      ▼
Freight Options
```

Regional freight must not be confused with Regional Product Pricing.

---

# 10. National Freight

National freight represents delivery possibilities across the supported national network.

It should not be modeled simply as:

```text
National = true
```

unless that is genuinely meaningful.

Eligibility should be determined from origin, destination, capabilities and logistics rules.

---

# 11. Carrier

Carrier represents a logistics provider capable of transporting an item.

Conceptually:

```text
Carrier
│
├── CarrierId
├── Name
└── Status
```

Examples may include external transport companies or internal logistics operations.

---

# 12. Carrier Identity

Yunu.Commerce should maintain canonical Carrier identity where carrier behavior becomes part of the Domain.

External carrier codes must be mapped through adapters.

---

# 13. Delivery Method

Potential delivery methods include:

```text
Standard
Express
SameDay
Scheduled
LocalDelivery
Pickup
```

Exact methods must follow actual commerce requirements.

---

# 14. Shipping Option

A ShippingOption represents one customer-selectable delivery alternative.

Conceptually:

```text
ShippingOption
│
├── DeliveryMethod
├── Carrier
├── FreightPrice
└── DeliveryPromise
```

A freight request may return multiple ShippingOptions.

---

# 15. Freight Price

FreightPrice represents the monetary cost of delivery.

It should use the shared financial semantics of:

```text
Money
Amount
Currency
```

Freight price is not Product Price.

---

# 16. Product Price vs Freight Price

These concepts must remain separate.

```text
Pricing
    → How much does the Offer cost?

Freight
    → How much does delivery cost?
```

A checkout read model may combine them, but their Domain ownership remains separate.

---

# 17. Free Freight

Free freight may result in:

```text
FreightPrice = 0
```

but zero freight may be produced by:

```text
commercial policy
promotion
seller policy
regional rule
customer benefit
```

The source of free freight must remain explicit when business behavior requires it.

---

# 18. Delivery SLA

DeliverySla represents the expected delivery duration.

Potential representation:

```text
MinimumBusinessDays
MaximumBusinessDays
```

Example:

```text
2 to 4 business days
```

Calendar-day and business-day semantics must never be mixed implicitly.

---

# 19. Delivery Promise

DeliveryPromise represents the customer-facing expected delivery date/window.

Conceptually:

```text
EstimatedDeliveryFrom
EstimatedDeliveryTo
```

Delivery Promise may depend on:

```text
current date/time
cutoff
handling time
carrier SLA
holidays
weekends
destination
origin
```

---

# 20. SLA vs Promise

SLA and Delivery Promise are related but different.

Example:

```text
SLA:
2 business days

Order/Quote date:
Monday

Promise:
Wednesday
```

Calendar resolution turns SLA into an actual promise.

---

# 21. Cutoff Time

Fulfillment operations may have cutoff times.

Example:

```text
Orders before 14:00
→ dispatch today

Orders after 14:00
→ dispatch next business day
```

Cutoff semantics may belong to Freight, Fulfillment policy, or orchestration depending on requirements.

Do not hardcode them before explicit modeling.

---

# 22. Handling Time

A Seller or Fulfillment Node may require handling time before carrier dispatch.

Potential concept:

```text
HandlingTime
```

This may be especially relevant for 3P Sellers.

Ownership must be decided from actual business behavior.

---

# 23. Business Calendar

Delivery promise calculation may require:

```text
weekends
national holidays
regional holidays
carrier operating days
```

A Business Calendar capability should be abstracted from provider-specific implementations.

---

# 24. Freight Request

A freight calculation request may conceptually contain:

```text
OfferId
SkuId
Quantity
Destination
CandidateFulfillmentNodes
ProductLogisticsData
```

The exact Application contract will be defined later.

---

# 25. Product Logistics Data

Freight may require Product/SKU physical characteristics such as:

```text
Weight
Height
Width
Length
Volume
PackageCount
SpecialHandling
```

Catalog may own canonical descriptive dimensions.

Freight consumes the required projection/contract.

---

# 26. Weight

Weight must be represented with explicit units.

Conceptually:

```text
Weight
Value
Unit
```

Avoid ambiguous values such as:

```text
Weight = 10
```

without knowing whether that means grams or kilograms.

---

# 27. Dimensions

Dimensions should use explicit measurement semantics.

Conceptually:

```text
Dimensions
Height
Width
Length
Unit
```

Provider-specific unit conversion belongs at integration boundaries.

---

# 28. Volumetric Weight

Carriers may use volumetric weight.

Conceptually:

```text
VolumetricWeight
=
function of dimensions and carrier rules
```

The exact formula may vary by Carrier.

Carrier-specific rules should be encapsulated behind adapters/policies rather than contaminating the canonical model.

---

# 29. Availability Boundary

Freight must not quote from arbitrary origins that cannot supply the item.

Conceptually:

```text
Availability
      │
      ▼
Available Fulfillment Nodes
      │
      ▼
Freight
```

Availability answers whether supply exists.

Freight evaluates delivery from eligible supply origins.

---

# 30. Fulfillment Boundary

Fulfillment owns:

```text
FulfillmentNode
Location
Capabilities
Service Areas
Status
```

Freight consumes the information necessary to evaluate delivery.

Freight must not directly modify Fulfillment Nodes.

---

# 31. Catalog Boundary

Catalog owns:

```text
Product
SKU
Weight
Dimensions
special descriptive logistics attributes
```

Freight consumes only the logistics data it needs.

It must not import the Catalog Aggregate.

---

# 32. Sellers Boundary

Sellers owns Seller identity and lifecycle.

Freight may require Seller-specific logistics policy for 3P commerce.

Seller lifecycle remains outside Freight.

---

# 33. Offers Boundary

Offer represents:

```text
Seller + SKU commercial relationship
```

Freight may calculate delivery for an Offer.

Conceptually:

```text
OfferId
   │
   ▼
Freight Quote
```

---

# 34. Pricing Boundary

Pricing owns Product/Offer commercial prices.

Freight owns delivery prices.

A freight discount triggered by Product price thresholds may require cross-context orchestration or promotion policy.

Avoid making Freight query Pricing persistence directly.

---

# 35. 1P Freight

First-party freight may originate from internal:

```text
Distribution Centers
Warehouses
Stores
Branches
```

Freight evaluates available origins and delivery options.

---

# 36. 3P Freight

Third-party Sellers may use:

```text
Seller-managed freight
Marketplace-managed freight
Platform-managed freight
Hybrid freight
```

The architecture must support these models through explicit policies and adapters.

---

# 37. Seller-Managed Freight

In seller-managed freight, the Seller or marketplace integration may provide:

```text
cost
SLA
delivery method
```

Yunu.Commerce still translates the result into its canonical Freight model.

---

# 38. Platform-Managed Freight

In platform-managed freight, Yunu.Commerce may directly integrate with carriers or logistics providers.

Provider-specific APIs remain Infrastructure adapters.

---

# 39. Carrier Adapter

Hexagonal Architecture requires carrier integrations to be adapters.

Conceptually:

```text
Freight Application
       │
       ▼
Carrier Quotation Port
       │
       ├── Carrier A Adapter
       ├── Carrier B Adapter
       └── Logistics Platform Adapter
```

The Domain must not reference carrier SDKs.

---

# 40. Carrier Port

A potential outbound port may conceptually provide:

```text
QuoteAsync(...)
```

The exact interface will be defined during Application/Infrastructure design.

It should expose canonical request/response contracts rather than carrier-specific DTOs.

---

# 41. Anti-Corruption Layer

External carrier models must be translated.

Conceptually:

```text
Carrier API Response
        │
        ▼
Carrier Adapter
        │
        ▼
Anti-Corruption Mapping
        │
        ▼
Canonical Shipping Option
        │
        ▼
Freight Application
```

---

# 42. Multiple Carriers

A freight request may query multiple providers.

Conceptually:

```text
Freight Request
      │
      ├── Carrier A
      ├── Carrier B
      └── Carrier C
            │
            ▼
     Shipping Options
```

The Application layer may orchestrate these calls.

---

# 43. Parallel Quotation

Independent carrier quotations may be executed concurrently.

This is an Application/Infrastructure optimization.

It must not alter Domain semantics.

---

# 44. Carrier Timeout

External carrier APIs may fail or respond slowly.

Adapters should support resilience policies such as:

```text
timeout
retry
circuit breaker
bulkhead
```

These belong to Infrastructure.

The Domain must not reference Polly or equivalent libraries.

---

# 45. Partial Carrier Failure

If one Carrier fails while others succeed, Freight may still return valid options depending on business policy.

Conceptually:

```text
Carrier A → success
Carrier B → timeout
Carrier C → success
```

The final behavior must be explicitly defined.

---

# 46. Freight Eligibility

Not every origin can serve every destination.

Eligibility may depend on:

```text
Fulfillment Node capability
Service Area
Carrier coverage
Product restrictions
Seller policy
Destination
Delivery method
```

Eligibility must be deterministic.

---

# 47. Freight Restrictions

Potential restrictions include:

```text
maximum weight
maximum dimensions
hazardous item
fragile item
restricted region
carrier limitation
seller restriction
```

Only actual business requirements should become Domain rules.

---

# 48. Origin Selection

Multiple available origins may be candidates.

Example:

```text
SKU available at:

DC-SP
Store-SP
DC-MG
```

Freight may need to evaluate each candidate.

---

# 49. Origin Optimization

Future selection strategies may include:

```text
lowest freight cost
fastest delivery
nearest origin
stock concentration
preferred node
operational priority
```

Complex optimization should not be placed inside a simple FreightQuote Entity without justification.

---

# 50. Split Shipment

An order containing multiple items may require multiple origins.

Conceptually:

```text
Order Basket

SKU A → DC-SP
SKU B → DC-MG
```

This introduces shipment orchestration complexity.

It is outside the initial Freight slice.

---

# 51. Multi-Item Freight

Calculating freight for a cart differs from calculating freight for a single Offer/SKU.

Future cart freight may require:

```text
package consolidation
split shipments
multiple sellers
multiple origins
carrier combinations
```

This may justify a dedicated checkout/shipping orchestration capability.

---

# 52. Pickup

Pickup is not traditional carrier freight, but it is a customer fulfillment option.

Conceptually:

```text
Customer
   │
   ▼
Pickup-capable Nodes
   │
   ▼
Availability
   │
   ▼
Pickup Options
```

Whether Pickup belongs directly to Freight or a broader Delivery Options capability will be refined later.

---

# 53. Regional Resolution

Conceptually:

```text
Destination Postal Code
        │
        ▼
Geographic Resolution
        │
        ▼
Region / Service Area
        │
        ▼
Eligible Origins
        │
        ▼
Freight Options
```

Geographic lookup should be abstracted behind appropriate ports.

---

# 54. Postal Code Resolution Port

A potential outbound port may resolve:

```text
PostalCode
→
City
State
Region
GeoLocation
```

The Domain must not depend on a specific postal-code API.

---

# 55. Freight Cache

Freight quotation can be expensive.

Redis may cache suitable quotation components or stable delivery rules.

Conceptually:

```text
Freight Request
      │
      ▼
Cache
      │
      ├── Hit → candidate response
      └── Miss → quotation workflow
```

Caching must respect quote freshness and context.

---

# 56. Cache Key

Potential freight-cache dimensions include:

```text
Origin
Destination
SKU logistics profile
Seller
Delivery method
Carrier
```

Cache design belongs to Infrastructure.

The Domain must not know Redis keys.

---

# 57. Cache Validity

Freight quotes may become stale because of:

```text
carrier rate changes
service-area changes
operational changes
fuel surcharges
Seller rules
```

TTL and invalidation strategy must be explicit.

---

# 58. Search Boundary

Search may expose simplified delivery information such as:

```text
freeShipping
sameDayEligible
pickupAvailable
```

These are projections.

Elasticsearch must not be used as the authoritative Freight quotation engine.

---

# 59. AI Boundary

AI may assist future logistics capabilities such as:

```text
delivery anomaly detection
carrier performance analysis
route recommendations
SLA prediction
freight optimization
```

AI recommendations must not bypass deterministic commerce rules.

Provider-specific AI implementation remains outside Freight Domain.

---

# 60. Aggregate Root Candidate

Potential Aggregate Root:

```text
FreightQuote
```

However, many quotations may be ephemeral rather than long-lived persisted Aggregates.

The architecture must decide whether quotes require canonical persistence.

---

# 61. Ephemeral Quote

A FreightQuote may be calculated and returned without long-term persistence.

Potential reasons to persist include:

```text
checkout consistency
audit
price guarantee
customer support
carrier reconciliation
```

This decision must follow actual requirements.

---

# 62. Quote Validity

Freight quotes may require an expiration time.

Conceptually:

```text
QuotedAtUtc
ValidUntilUtc
```

A checkout may need to reject or recalculate expired quotes.

---

# 63. Freight Quote Identity

If persisted or referenced by Checkout, FreightQuote should have:

```text
FreightQuoteId
```

The identity must be canonical and provider-independent.

---

# 64. Value Object Candidates

Potential Value Objects include:

```text
FreightQuoteId
PostalCode
Destination
FreightPrice
DeliverySla
DeliveryPromise
Weight
Dimensions
CarrierId
DeliveryMethod
RegionId
```

Value Objects should protect meaningful logistics semantics.

---

# 65. Repository Boundary

If freight quotes or freight policies are persisted, potential repository ports may include:

```text
IFreightQuoteRepository
IFreightPolicyRepository
```

Do not create repositories before actual persistence needs are known.

---

# 66. Persistence Independence

Freight Domain must remain independent from persistence technology.

Potential storage may include:

```text
SQL Server
PostgreSQL
MongoDB
Redis
```

Each technology must have an explicit role.

---

# 67. Application Use Cases

Potential use cases include:

```text
QuoteFreight
GetDeliveryOptions
GetPickupOptions
ResolveEligibleOrigins
GetFreightQuote
RecalculateFreight
```

Future capabilities may include:

```text
QuoteCartFreight
OptimizeShipment
SplitShipment
```

only when explicitly designed.

---

# 68. CQRS

Freight may use CQRS where useful.

Commands/actions:

```text
CreateFreightPolicy
UpdateFreightPolicy
```

Queries/calculations:

```text
QuoteFreight
GetDeliveryOptions
GetFreightQuote
```

Not every calculation needs to become a persisted command.

---

# 69. Freight Quotation Flow

Conceptually:

```text
Offer / SKU
+
Destination
+
Quantity
      │
      ▼
Availability
      │
      ▼
Available Fulfillment Nodes
      │
      ▼
Fulfillment Eligibility
      │
      ▼
Freight Application
      │
      ├── Carrier Adapter A
      ├── Carrier Adapter B
      └── Carrier Adapter C
             │
             ▼
       Canonical Options
             │
             ▼
       Freight Rules
             │
             ▼
       Shipping Options
```

---

# 70. Regional Freight Flow

Conceptually:

```text
Destination CEP
      │
      ▼
Region Resolution
      │
      ▼
Eligible Nodes
      │
      ▼
Available Nodes
      │
      ▼
Carrier Coverage
      │
      ▼
Regional Freight Options
```

---

# 71. Domain Events

Potential Freight Domain Events include:

```text
FreightQuoteCreatedDomainEvent
FreightQuoteExpiredDomainEvent
FreightPolicyChangedDomainEvent
DeliveryOptionChangedDomainEvent
```

Only meaningful stateful Domain behavior should produce Domain Events.

---

# 72. Integration Events

Potential Integration Events include:

```text
FreightPolicyChanged
CarrierAvailabilityChanged
FreightQuoteCreated
```

Avoid publishing enormous volumes of ephemeral quotation events unless a consumer genuinely requires them.

---

# 73. Carrier Events

Carrier integration health is primarily an operational concern.

Events such as:

```text
CarrierApiTimeout
CarrierCircuitOpened
```

should generally be telemetry rather than Domain Events.

Do not confuse operational incidents with business facts.

---

# 74. Idempotency

Freight requests may use:

```text
CorrelationId
RequestId
QuoteId
```

where idempotent behavior is required.

External carrier APIs may have their own idempotency semantics handled by adapters.

---

# 75. Concurrency

Most freight quotation operations are read/calculation heavy rather than shared-state mutation heavy.

Concurrency concerns are therefore more likely to involve:

```text
parallel provider calls
cache consistency
policy updates
quote validity
```

rather than Aggregate locking.

---

# 76. Auditability

Where Freight Quotes are commercially binding, audit metadata may include:

```text
QuotedAtUtc
ValidUntilUtc
Carrier
Origin
Destination
Price
SLA
Source
CorrelationId
```

The required audit level must be explicitly defined.

---

# 77. Observability

Freight integrations require strong telemetry.

Important metrics may include:

```text
quotation latency
carrier latency
carrier timeout rate
carrier error rate
cache hit rate
quote success rate
no-delivery-option rate
circuit breaker state
```

OpenTelemetry belongs to Infrastructure/Application instrumentation.

---

# 78. Security

Freight APIs may expose sensitive location or commercial information.

Authorization requirements may differ between:

```text
public quotation
checkout
internal operations
carrier callbacks
seller integrations
```

Security belongs to Host/Application boundaries.

---

# 79. Error Semantics

Potential meaningful errors include:

```text
InvalidDestination
InvalidPostalCode
NoAvailableFulfillmentOrigin
NoDeliveryOption
FreightQuoteExpired
CarrierUnavailable
InvalidProductLogisticsData
UnsupportedDeliveryRegion
```

Carrier-specific exceptions must be translated into canonical Application/Domain outcomes.

---

# 80. Validation Layers

Application validation may verify:

```text
required request fields
postal-code format
quantity format
```

Domain validation protects:

```text
freight invariants
delivery eligibility
quote validity
canonical logistics semantics
```

Infrastructure validation protects:

```text
carrier API requirements
serialization
network concerns
provider-specific limits
```

---

# 81. Testing Strategy

Freight tests should cover:

```text
origin eligibility
destination validation
regional freight rules
freight price semantics
SLA semantics
delivery promise calculation
quote expiration
carrier result normalization
partial provider failure policy
no-option scenarios
```

Provider adapters require dedicated integration/contract tests.

---

# 82. Architecture Questions Before Implementation

Before implementing Freight, explicitly decide:

```text
What information is required to quote freight?

Is freight quoted primarily by OfferId, SkuId or both?

How are candidate origins obtained?

Does Freight call Availability synchronously or consume a projection?

How are Regions defined?

Is CEP the primary destination key in Brazil?

Who owns postal-code geographic resolution?

Which Delivery Methods exist initially?

Which carriers/providers will be integrated first?

How is 1P freight different from 3P freight?

Can 3P Sellers provide their own freight?

What happens when one carrier times out?

Are carrier calls executed in parallel?

What timeout budget applies?

What retry policy applies?

Are Freight Quotes persisted?

How long is a quote valid?

How is free freight represented?

How are business days and holidays calculated?

Where do handling time and cutoff rules belong?

How is freight cached?

What freight information belongs in Elasticsearch?

Do we initially support single-item freight only?
```

These decisions must be explicit before detailed implementation.

---

# 83. Initial Implementation Scope

The first Freight implementation should remain intentionally small.

Recommended initial slice:

```text
FreightQuote
FreightQuoteId
Origin
Destination
PostalCode
FreightPrice
DeliverySla
DeliveryMethod
CarrierId
Quote validity
carrier quotation port
one fake/test carrier adapter
unit tests
```

The first implementation should initially focus on:

```text
single Offer/SKU
single quantity
one destination
eligible fulfillment origins
canonical freight response
```

Do not implement complex cart splitting or logistics optimization in the first slice.

---

# 84. Relationship with Catalog

Catalog answers:

> What is the Product/SKU and what are its logistics characteristics?

Freight consumes only the physical/logistics data required for delivery calculation.

---

# 85. Relationship with Sellers

Sellers answers:

> Who is selling?

Freight may apply Seller-specific logistics models without owning Seller lifecycle.

---

# 86. Relationship with Offers

Offers answers:

> What Seller-SKU commercial relationship exists?

Freight may quote delivery for that Offer.

---

# 87. Relationship with Pricing

Pricing answers:

> How much does the item cost?

Freight answers:

> How much does delivery cost?

The checkout experience may combine both.

---

# 88. Relationship with Availability

Availability answers:

> Which eligible nodes currently have supply?

Freight should not offer delivery from unavailable origins.

---

# 89. Relationship with Fulfillment

Fulfillment answers:

> What nodes exist, where are they, and what capabilities/service areas do they have?

Freight uses eligible origins to calculate delivery options.

---

# 90. Relationship with Search

Search may expose simplified delivery signals.

Freight remains authoritative for actual quotation.

---

# 91. Checkout Boundary

Checkout will eventually combine:

```text
Offer
Price
Availability
Fulfillment
Freight
Payment
```

Checkout orchestration must not cause Freight to absorb all of these contexts.

---

# 92. Buyability and Deliverability

Buyability and deliverability are related but distinct.

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

Deliverable
=
Buyable
+
Eligible Fulfillment Origin
+
Valid Freight Option
```

Pickup may follow a different fulfillment path.

---

# 93. Data Ownership

Freight is authoritative for:

```text
freight business rules
canonical freight quotations
delivery methods
delivery SLA semantics
delivery promise semantics
freight price semantics
freight eligibility rules
```

Carrier systems remain authoritative for provider-specific capabilities and rates where applicable.

---

# 94. No Shared Database Ownership

Freight must not directly update:

```text
Catalog
Offers
Pricing
Availability
Fulfillment
```

and those contexts must not directly modify Freight persistence.

Communication occurs through explicit ports, APIs, events and projections.

---

# 95. Domain Purity

Freight Domain must not reference:

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
Polly
carrier SDKs
HTTP clients
```

The Domain must remain independently testable.

---

# 96. Hexagonal Principle

External logistics systems are replaceable adapters.

Conceptually:

```text
                 ┌─────────────────────┐
                 │   Freight Domain    │
                 └──────────┬──────────┘
                            │
                         Ports
                            │
          ┌─────────────────┼─────────────────┐
          ▼                 ▼                 ▼
      Carrier A         Carrier B        Geo Provider
       Adapter           Adapter            Adapter
```

Changing a carrier must not require redesigning the Freight Domain.

---

# 97. Evolution Principle

Freight will evolve incrementally.

Avoid prematurely implementing:

```text
multi-item shipment optimization
route optimization
fleet management
dynamic carrier bidding
machine-learning SLA prediction
international customs
complex tax logistics
last-mile fleet tracking
shipment execution
```

These capabilities may eventually justify separate Bounded Contexts.

---

# 98. Core Rule

Freight owns the canonical answer to:

> How can an available commercial item be delivered from an eligible fulfillment origin to the requested destination, at what cost and within what promise?

It does not answer:

```text
What is the product?             -> Catalog
Who is selling it?               -> Sellers
What is the commercial Offer?    -> Offers
How much does the item cost?     -> Pricing
Is supply available?             -> Availability
What fulfillment nodes exist?    -> Fulfillment
```

---

# 99. Final Domain Map

With Freight defined, the initial Yunu.Commerce commerce domains are:

```text
Catalog
   │
   │ SKU
   ▼
Offers ◄──────── Sellers
   │
   ├────────────► Pricing
   │
   └────────────► Availability
                       │
                       ▼
                  Fulfillment
                       │
                       ▼
                    Freight
```

The relationships are logical dependencies and integration flows, not direct Domain-project references.

---

# 100. Commerce Questions

The initial Bounded Contexts now answer distinct questions:

```text
Catalog
→ What is the product?

Sellers
→ Who is selling?

Offers
→ What commercial Seller-SKU relationship exists?

Pricing
→ How much does the Offer cost?

Availability
→ Can the item currently be supplied?

Fulfillment
→ From which locations can it be fulfilled?

Freight
→ How can it be delivered, at what freight cost and SLA?
```

This separation is fundamental to Yunu.Commerce.

---

# 101. Final Principle

The Freight Domain protects delivery calculation and logistics semantics from external carrier complexity.

It must remain:

```text
business-focused
origin-aware
destination-aware
region-aware
carrier-independent
availability-aware but availability-independent
fulfillment-aware but fulfillment-independent
pricing-independent
search-independent
cache-independent
database-independent
broker-independent
cloud-independent
AI-provider-independent
```

Carriers may change.

Freight providers may change.

Postal-code providers may change.

Cloud infrastructure may change.

The canonical Freight business model and delivery semantics must remain protected.
