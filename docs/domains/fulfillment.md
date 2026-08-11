# Yunu.Commerce - Fulfillment Domain

## 1. Purpose

This document defines the Fulfillment Bounded Context for Yunu.Commerce.

The Fulfillment Domain owns the canonical representation of the physical and logical nodes from which products can be fulfilled.

Fulfillment answers:

> From where can a commercial item be fulfilled, and what fulfillment capabilities does that location provide?

The platform must support fulfillment structures such as:

- Branches
- Physical stores
- Warehouses
- Distribution centers
- Marketplace fulfillment locations
- Pickup locations
- Ship-from-store locations
- Regional fulfillment networks
- Delivery-capable nodes

Fulfillment does not own:

- Product descriptive information
- SKU structure
- Seller lifecycle
- Offer lifecycle
- Product prices
- Payment prices
- Stock quantities
- Sellable availability
- Freight prices
- Carrier rates
- Search indexes
- AI provider implementation

---

# 2. Domain Responsibility

Fulfillment is responsible for:

- Fulfillment Node identity
- Fulfillment Node type
- Fulfillment Node lifecycle
- Branch representation
- Store representation
- Warehouse representation
- Distribution Center representation
- Fulfillment capabilities
- Geographic location
- Regional service relationships
- Pickup capability
- Ship-from-store capability
- Delivery capability
- Fulfillment eligibility metadata

Fulfillment must remain independent from:

- database technology
- Kafka
- Redis
- Elasticsearch
- Azure
- Google Cloud
- ERP schemas
- WMS schemas
- carrier APIs

---

# 3. Core Ubiquitous Language

The initial Fulfillment language includes:

```text
FulfillmentNode
FulfillmentNodeId
FulfillmentNodeType
FulfillmentNodeStatus
Branch
Store
Warehouse
DistributionCenter
PickupPoint
FulfillmentCapability
RegionId
Address
GeoLocation
ServiceArea
```

External terminology must be translated into this canonical language.

---

# 4. Fulfillment Node

FulfillmentNode is the canonical concept representing a location or logical source capable of participating in fulfillment.

Conceptually:

```text
FulfillmentNode
│
├── FulfillmentNodeId
├── Name
├── Type
├── Status
├── Location
└── Capabilities
```

Different external systems may call these locations:

```text
Branch
Store
Warehouse
DC
Stock Location
Depot
Hub
Pickup Point
```

Yunu.Commerce translates those concepts into its canonical model.

---

# 5. Fulfillment Node Identity

Every Fulfillment Node must have a canonical identity:

```text
FulfillmentNodeId
```

External identifiers must not replace this identity.

Potential external references include:

```text
ERP Branch Code
WMS Warehouse Code
Store Code
Marketplace Fulfillment Id
Legacy Location Id
```

---

# 6. Fulfillment Node Type

Potential initial node types include:

```text
Store
Branch
Warehouse
DistributionCenter
PickupPoint
MarketplaceNode
```

The exact list must follow actual business requirements.

Avoid creating separate Aggregates merely because physical locations have different labels.

---

# 7. Store

A Store represents a physical retail location.

A Store may support capabilities such as:

```text
CustomerPickup
ShipFromStore
LocalDelivery
```

A Store does not automatically support every capability.

---

# 8. Branch

Branch may represent an organizational or operational location.

Depending on the business model, Branch and Store may be:

```text
the same canonical FulfillmentNode with different Type
```

or genuinely different concepts.

This must be decided from actual business semantics rather than existing database naming.

---

# 9. Warehouse

A Warehouse represents a storage location capable of fulfilling inventory.

Potential capabilities include:

```text
Shipping
Transfer
RegionalDelivery
```

Availability owns the stock state within the Warehouse.

Fulfillment owns the Warehouse identity and capabilities.

---

# 10. Distribution Center

A Distribution Center is a specialized Fulfillment Node.

Potential characteristics include:

```text
large inventory capacity
regional service coverage
carrier integration
transfer operations
delivery dispatch
```

These operational characteristics should only become Domain properties when business behavior requires them.

---

# 11. Pickup Point

A Pickup Point represents a location where customers may collect orders.

A Pickup Point may or may not hold inventory itself.

This distinction is important.

Conceptually:

```text
Inventory Source
      │
      ▼
Pickup Point
```

may differ from:

```text
Pickup Point with local inventory
```

The model must not assume they are identical.

---

# 12. Fulfillment Capability

A Fulfillment Node may expose explicit capabilities.

Potential capabilities include:

```text
Shipping
CustomerPickup
ShipFromStore
LocalDelivery
Transfer
MarketplaceFulfillment
```

Capabilities describe what a node can do.

They do not describe whether a specific SKU currently has stock there.

---

# 13. Capability vs Availability

This distinction is fundamental.

Example:

```text
Store SP-001

Capability:
CustomerPickup = true

Availability:
SKU 123 = 0 units
```

The Store supports pickup, but SKU 123 is currently unavailable.

Fulfillment owns capability.

Availability owns supply state.

---

# 14. Fulfillment Node Status

Potential lifecycle states include:

```text
Draft
Active
Inactive
Suspended
Archived
```

Exact transitions must be explicitly defined.

---

# 15. Active Node

An Active Fulfillment Node may participate in fulfillment resolution.

Active does not imply:

```text
every SKU has stock
every destination is served
every carrier is available
```

Those decisions require other contexts.

---

# 16. Inactive Node

An Inactive node exists but should not participate in normal fulfillment selection.

Historical references must remain valid.

---

# 17. Suspended Node

A node may be temporarily suspended because of:

```text
operational outage
maintenance
capacity issue
integration problem
manual intervention
```

Exact suspension semantics will be modeled when required.

---

# 18. Archived Node

Archived nodes remain historically identifiable but are removed from normal fulfillment operations.

Physical deletion should not be the default when historical orders reference the node.

---

# 19. Node Lifecycle

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

Exact transitions must be approved before implementation.

---

# 20. Address

A Fulfillment Node may have an Address.

Potential concepts include:

```text
Country
State
City
District
Street
PostalCode
Number
Complement
```

Address should be modeled as a meaningful Value Object when required.

---

# 21. GeoLocation

A node may have geographic coordinates.

Conceptually:

```text
Latitude
Longitude
```

Coordinates may support:

```text
distance calculations
regional routing
nearby pickup
delivery optimization
```

Geospatial infrastructure must remain outside the Domain.

---

# 22. Region

Fulfillment may associate nodes with canonical commercial/service regions.

Conceptually:

```text
RegionId
```

Example:

```text
Southeast
South
Northeast
```

However, geographic service coverage may eventually be more precise than broad regions.

---

# 23. Service Area

A Service Area represents where a Fulfillment Node is eligible to provide a fulfillment capability.

Potential future representations include:

```text
Region
State
City
Postal Code Range
Geographic Polygon
Radius
```

The exact model should evolve from Freight and fulfillment requirements.

---

# 24. Region Membership vs Service Area

These concepts must not be confused.

A Warehouse may physically be located in:

```text
São Paulo
```

while serving:

```text
São Paulo
Rio de Janeiro
Minas Gerais
Paraná
```

Physical location is not the same as service coverage.

---

# 25. National Fulfillment

A node may participate in national fulfillment if its service capabilities and downstream Freight rules permit it.

Do not represent:

```text
National = true
```

unless that is genuinely a stable business concept.

Service coverage should remain explicit.

---

# 26. Regional Fulfillment

Regional fulfillment may use groups of eligible nodes.

Conceptually:

```text
Region Southeast
│
├── DC-SP-01
├── Store-SP-101
├── Store-RJ-210
└── DC-MG-01
```

Availability determines which of those nodes have supply.

---

# 27. Catalog Boundary

Fulfillment does not own:

```text
Product
SKU
Category
Brand
Attributes
Specifications
Media
```

Catalog owns product semantics.

---

# 28. Sellers Boundary

Fulfillment does not own Seller lifecycle.

A Seller may use one or multiple Fulfillment Nodes.

Conceptually:

```text
Seller
  │
  ├── Fulfillment Node A
  └── Fulfillment Node B
```

The exact Seller-to-node relationship must be defined from 1P/3P requirements.

---

# 29. Seller Fulfillment Relationship

Third-party Sellers may:

```text
fulfill themselves
use marketplace fulfillment
use platform fulfillment
use mixed fulfillment models
```

This relationship may eventually require explicit modeling.

Do not assume:

```text
Seller = FulfillmentNode
```

They are distinct concepts.

---

# 30. Offers Boundary

Offer represents:

```text
Seller + SKU commercial relationship
```

Fulfillment determines which nodes may participate in fulfilling that commerce.

Offer must not contain an ever-growing collection of Fulfillment Nodes.

---

# 31. Pricing Boundary

Fulfillment does not own product price.

Regional Pricing and Fulfillment may use related geographic identifiers, but they represent different business concerns.

---

# 32. Availability Boundary

Availability is one of Fulfillment's most important neighboring contexts.

Fulfillment owns:

```text
FulfillmentNode
Location
Capabilities
Status
Service Area
```

Availability owns:

```text
SKU/Offer supply
Quantity
Sellable Quantity
Available/Unavailable state
```

---

# 33. Availability Relationship

Conceptually:

```text
FulfillmentNode
      │
      │ FulfillmentNodeId
      ▼
Availability
      │
      ├── SKU A = 10
      ├── SKU B = 0
      └── SKU C = 3
```

The Fulfillment Aggregate must not contain stock collections.

---

# 34. Freight Boundary

Freight consumes Fulfillment information to determine delivery options.

Conceptually:

```text
Available Fulfillment Nodes
           │
           ▼
        Freight
           │
           ├── Carrier
           ├── Cost
           └── SLA
```

Fulfillment does not calculate Freight.

---

# 35. Freight Eligibility

A Fulfillment Node may be eligible for a destination based on:

```text
service area
node capability
node status
```

Freight then evaluates delivery-specific conditions.

The precise responsibility split must be refined during Freight modeling.

---

# 36. Search Boundary

Search may expose fulfillment-related projections such as:

```text
pickupAvailable
nearbyStoreAvailable
shipFromStore
```

These are customer-facing projections.

Elasticsearch does not own Fulfillment state.

---

# 37. Redis Boundary

Redis may cache:

```text
node metadata
service-area resolution
regional node lists
```

Redis is not the canonical Fulfillment store.

---

# 38. AI Boundary

AI may eventually assist with:

```text
fulfillment anomaly analysis
network optimization suggestions
service-area recommendations
capacity forecasting
```

AI must not bypass Fulfillment Domain rules.

Provider-specific AI implementation belongs outside the Domain.

---

# 39. Aggregate Root Candidate

The primary Aggregate Root candidate is:

```text
FulfillmentNode
```

Conceptually:

```text
FulfillmentNode
│
├── FulfillmentNodeId
├── Name
├── Type
├── Status
├── Address
├── GeoLocation
└── Capabilities
```

Service Areas may belong inside or outside the Aggregate depending on scale and concurrency.

---

# 40. Small Aggregate Principle

Avoid modeling:

```text
FulfillmentNode
└── all SKUs
    └── all stock
        └── all freight options
```

This would collapse multiple high-volume contexts into one Aggregate.

FulfillmentNode should remain focused on node identity and behavior.

---

# 41. Value Object Candidates

Potential Value Objects include:

```text
FulfillmentNodeId
FulfillmentNodeName
Address
GeoLocation
RegionId
ServiceArea
FulfillmentCapability
ExternalFulfillmentReference
```

Only introduce Value Objects when they provide meaningful semantics or invariants.

---

# 42. External Fulfillment References

External systems may identify nodes differently.

Conceptually:

```text
ExternalFulfillmentReference

System
Type
Value
```

Examples:

```text
ERP Branch Code
WMS Location Code
Legacy Store Id
Marketplace Warehouse Id
```

External identity must not replace FulfillmentNodeId.

---

# 43. Anti-Corruption Layer

External location models must be translated.

Conceptually:

```text
ERP Branch
WMS Warehouse
Marketplace Location
       │
       ▼
Integration Adapter
       │
       ▼
Anti-Corruption Layer
       │
       ▼
Canonical Fulfillment Input
       │
       ▼
Fulfillment Application
```

---

# 44. Repository Boundary

Potential repository contract:

```text
IFulfillmentNodeRepository
```

Repository contracts must not expose:

```text
DbContext
MongoCollection
SQL Connection
Redis
Elasticsearch
```

Infrastructure implements persistence adapters.

---

# 45. Persistence Independence

Fulfillment Domain must remain independent from persistence technology.

Potential persistence options include:

```text
SQL Server
PostgreSQL
MongoDB
```

A relational database is a strong initial candidate because node identity, capabilities and location metadata are structured and constraint-oriented.

The final choice belongs to Data Architecture.

---

# 46. Geospatial Data

Service-area and location queries may eventually require geospatial capabilities.

Possible infrastructure choices include:

```text
PostGIS
SQL spatial types
Elasticsearch geo queries
specialized geospatial services
```

The Domain must not depend on a particular geospatial engine.

---

# 47. CQRS

Fulfillment may use CQRS.

Commands:

```text
CreateFulfillmentNode
ActivateFulfillmentNode
DeactivateFulfillmentNode
SuspendFulfillmentNode
AddCapability
RemoveCapability
ChangeServiceArea
```

Queries:

```text
GetFulfillmentNode
GetNodesByRegion
GetNodesByCapability
GetNodesServingDestination
```

---

# 48. Application Use Cases

Potential use cases include:

```text
CreateFulfillmentNode
UpdateFulfillmentNode
ActivateFulfillmentNode
DeactivateFulfillmentNode
SuspendFulfillmentNode
ReactivateFulfillmentNode
ArchiveFulfillmentNode
AddCapability
RemoveCapability
UpdateLocation
ConfigureServiceArea
GetEligibleFulfillmentNodes
```

Use cases should be introduced incrementally.

---

# 49. Domain Events

Potential Domain Events include:

```text
FulfillmentNodeCreatedDomainEvent
FulfillmentNodeActivatedDomainEvent
FulfillmentNodeDeactivatedDomainEvent
FulfillmentNodeSuspendedDomainEvent
FulfillmentNodeReactivatedDomainEvent
FulfillmentCapabilityAddedDomainEvent
FulfillmentCapabilityRemovedDomainEvent
ServiceAreaChangedDomainEvent
```

Exact events must emerge from actual behavior.

---

# 50. Integration Events

Potential Integration Events include:

```text
FulfillmentNodeCreated
FulfillmentNodeActivated
FulfillmentNodeDeactivated
FulfillmentNodeSuspended
FulfillmentNodeUpdated
FulfillmentCapabilityChanged
FulfillmentServiceAreaChanged
```

Potential consumers include:

```text
Availability
Freight
Search
Commerce projections
Analytics
```

---

# 51. Node Suspension Event

A node suspension may affect Availability and Freight.

Conceptually:

```text
FulfillmentNodeSuspended
          │
          ▼
        Kafka
          │
          ├── Availability reacts
          ├── Freight reacts
          └── Search projection reacts
```

Fulfillment must not directly update those contexts' databases.

---

# 52. Eventual Consistency

Node changes propagate asynchronously where appropriate.

Example:

```text
Node suspended
      │
      ▼
Fulfillment state updated
      │
      ▼
Integration Event
      │
      ├── Availability projection
      ├── Freight projection
      └── Search projection
```

Critical runtime decisions may additionally validate node eligibility synchronously or through local projections.

---

# 53. Transactional Outbox

Fulfillment changes that publish Integration Events should use the Transactional Outbox pattern where appropriate.

Conceptually:

```text
Fulfillment transaction
        │
        ├── Persist Node
        └── Persist Outbox
                 │
                 ▼
               Kafka
```

---

# 54. Inbox and Idempotency

Consumers of external Fulfillment updates must tolerate duplicate messages.

Potential strategies include:

```text
Inbox
MessageId
ExternalVersion
IdempotencyKey
```

---

# 55. External Synchronization

Fulfillment Nodes may be synchronized from enterprise systems.

Potential sources:

```text
ERP
WMS
Store master
Marketplace
Logistics platform
```

Synchronization must preserve canonical identity.

---

# 56. Source of Truth

The architecture must explicitly decide which system owns creation and lifecycle for each type of Fulfillment Node.

Examples:

```text
ERP may own branch master data
WMS may own warehouse operational data
Yunu.Commerce may own commerce capabilities
```

An Anti-Corruption Layer should combine these concerns without surrendering the canonical Domain model.

---

# 57. Concurrency

Fulfillment Node metadata changes relatively less frequently than Availability.

Optimistic concurrency is likely sufficient for many node-management workflows.

Exact concurrency strategy belongs to implementation.

---

# 58. Auditability

Potential audit metadata includes:

```text
CreatedAtUtc
UpdatedAtUtc
CreatedBy
UpdatedBy
Source
CorrelationId
```

Changes to operational capabilities may require historical traceability.

---

# 59. Security

Fulfillment management is an administrative capability.

Potential actors include:

```text
Logistics administrator
Store administrator
Integration identity
Operations system
```

Authorization belongs to Application/Host boundaries.

The Domain must remain security-framework independent.

---

# 60. Error Semantics

Potential meaningful errors include:

```text
FulfillmentNodeNotFound
InvalidFulfillmentNodeType
InvalidFulfillmentNodeState
FulfillmentNodeAlreadyActive
FulfillmentNodeSuspended
InvalidGeoLocation
InvalidServiceArea
CapabilityAlreadyExists
CapabilityNotSupported
DuplicateExternalFulfillmentReference
```

Infrastructure exceptions must not leak directly into API semantics.

---

# 61. Validation Layers

Application validation may verify:

```text
required fields
input shape
identifier format
```

Domain validation protects:

```text
node invariants
lifecycle transitions
capability rules
service-area consistency
```

Infrastructure validation protects:

```text
database constraints
external provider requirements
serialization
```

---

# 62. Testing Strategy

Fulfillment Domain tests should focus on:

```text
node creation
activation
deactivation
suspension
reactivation
capability management
invalid lifecycle transitions
service-area rules
location invariants
Domain Events
```

Pure Domain tests should require no Infrastructure mocks.

---

# 63. Fulfillment Resolution

A future fulfillment-resolution workflow may combine:

```text
Destination
Offer
SKU
Seller
Available Nodes
Node Capabilities
Service Areas
```

Conceptually:

```text
Customer Destination
        │
        ▼
Eligible Service Areas
        │
        ▼
Eligible Fulfillment Nodes
        │
        ▼
Availability
        │
        ▼
Nodes with Supply
        │
        ▼
Freight
```

This cross-context workflow should not be forced into the FulfillmentNode Aggregate.

---

# 64. Fulfillment Orchestration

Cross-context fulfillment resolution belongs in an Application/orchestration layer or dedicated commerce capability.

It should coordinate domain outputs without creating direct Domain-project dependencies.

---

# 65. Pickup Flow

Conceptually:

```text
Customer Location
      │
      ▼
Pickup-capable Nodes
      │
      ▼
Availability
      │
      ▼
Nodes with SKU available
      │
      ▼
Pickup Options
```

Fulfillment provides node capability and location.

Availability provides stock state.

---

# 66. Ship-from-Store Flow

Conceptually:

```text
Destination
    │
    ▼
ShipFromStore-capable Nodes
    │
    ▼
Availability
    │
    ▼
Nodes with supply
    │
    ▼
Freight
    │
    ▼
Delivery options
```

---

# 67. Marketplace Fulfillment

A 3P Seller may use:

```text
Seller-managed fulfillment
Platform-managed fulfillment
Hybrid fulfillment
```

The architecture should support these models without changing Catalog identity.

---

# 68. 1P Fulfillment

First-party commerce may use internal:

```text
Distribution Centers
Warehouses
Stores
Branches
```

The same canonical FulfillmentNode abstraction should be reused where business semantics align.

---

# 69. Capacity

Future fulfillment logic may consider operational capacity:

```text
orders per hour
picking capacity
delivery capacity
temporary throttling
```

Capacity is not part of the initial model.

If it becomes significant, it may require its own capability/model.

---

# 70. Node Priority

Future fulfillment selection may use priority.

Examples:

```text
preferred DC
nearest node
lowest freight cost
fastest SLA
stock concentration
operational priority
```

Do not place these algorithms inside FulfillmentNode prematurely.

They likely belong to orchestration/optimization behavior.

---

# 71. Distance

Distance between customer and Fulfillment Node may influence:

```text
pickup suggestions
local delivery
freight
routing
```

Fulfillment owns location data.

Distance calculation may be performed by a dedicated infrastructure/geospatial adapter.

---

# 72. Data Ownership

Fulfillment is authoritative for:

```text
Fulfillment Node identity
Fulfillment Node type
Fulfillment Node lifecycle
Node location
Node capabilities
Service-area configuration
External node references
```

Other contexts must not directly modify Fulfillment persistence.

---

# 73. No Shared Database Ownership

Even if Fulfillment and Availability use the same database technology, ownership remains separate.

Forbidden:

```text
Availability directly updating FulfillmentNode status
```

Forbidden:

```text
Freight directly changing node capabilities
```

Communication must use explicit boundaries.

---

# 74. Domain Purity

Fulfillment Domain must not reference:

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
carrier SDKs
HTTP clients
```

The Domain must remain independently testable.

---

# 75. Architecture Questions Before Implementation

Before implementing Fulfillment, explicitly decide:

```text
What is the exact definition of FulfillmentNode?

Are Branch and Store separate concepts or node types?

Which node types exist initially?

Can one Seller use multiple Fulfillment Nodes?

Can multiple Sellers share a Fulfillment Node?

How is 1P fulfillment represented?

How is 3P fulfillment represented?

Which capabilities are required initially?

What defines a Region?

What defines a Service Area?

Can a node serve regions different from its physical region?

Who owns node master data?

What happens to Availability when a node is suspended?

What happens to Freight when a node is suspended?

Do we need geospatial queries initially?

How should external ERP/WMS node identities be mapped?
```

These decisions must be explicit before detailed implementation.

---

# 76. Initial Implementation Scope

The first Fulfillment implementation should remain intentionally small.

Recommended initial slice:

```text
FulfillmentNode
FulfillmentNodeId
FulfillmentNodeType
FulfillmentNodeStatus
Name
Address
RegionId
basic capabilities
ExternalFulfillmentReference
creation
activation
deactivation
Domain Events
repository port
unit tests
```

Do not implement complex routing, capacity optimization or geospatial service areas in the first slice.

---

# 77. Relationship with Catalog

Catalog answers:

> What is the Product/SKU?

Fulfillment does not duplicate Catalog data.

---

# 78. Relationship with Sellers

Sellers answers:

> Who is the Seller?

Fulfillment answers:

> Which physical/logical nodes can participate in fulfillment?

Seller and FulfillmentNode remain separate identities.

---

# 79. Relationship with Offers

Offers answers:

> What Seller-SKU commercial relationship exists?

Fulfillment participates in determining where that Offer may be fulfilled.

---

# 80. Relationship with Pricing

Pricing answers:

> How much does the Offer cost?

Fulfillment does not own product pricing.

---

# 81. Relationship with Availability

Availability answers:

> What supply exists at a Fulfillment Node?

Fulfillment answers:

> What is that node, where is it, and what can it do?

---

# 82. Relationship with Freight

Freight answers:

> How can an available item move from an eligible Fulfillment Node to the destination, at what cost and SLA?

Fulfillment provides origin identity, location and capability.

---

# 83. Relationship with Search

Search may project simplified fulfillment information.

Examples:

```text
pickupAvailable
nearestPickupStore
shipFromStoreAvailable
```

These are projections and must not become canonical Fulfillment state.

---

# 84. Buyability

Fulfillment is one component of the broader buyability decision.

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
+
Delivery or Pickup Option
```

Fulfillment must not absorb all commerce logic.

---

# 85. Final Principle

The Fulfillment Domain owns the canonical topology and capabilities of the commerce fulfillment network.

It must remain:

```text
business-focused
location-aware
capability-aware
seller-compatible
availability-independent
pricing-independent
freight-independent
search-independent
database-independent
broker-independent
cloud-independent
AI-provider-independent
```

ERP systems may change.

WMS systems may change.

Warehouses and stores may change.

Delivery providers may change.

The canonical Fulfillment Node model and its business semantics must remain protected.
