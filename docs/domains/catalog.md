# Yunu.Commerce - Catalog Domain

## 1. Purpose

This document defines the Catalog Bounded Context for Yunu.Commerce.

The Catalog Domain owns the canonical representation of products and their sellable variations inside Yunu.Commerce.

Catalog answers the question:

> What is this product?

The Catalog Domain describes product identity, classification, structure and descriptive information.

It does not own:

- sellers
- offers
- prices
- availability
- inventory
- freight
- payment conditions

Those concerns belong to other Bounded Contexts.

---

# 2. Domain Responsibility

Catalog is responsible for:

- Product identity
- SKU identity
- Product structure
- Product variations
- Product classification
- Brand association
- Product attributes
- Technical specifications
- Product media
- Product lifecycle
- Catalog publication state
- Product descriptive metadata
- AI enrichment approval state

Catalog must preserve a canonical business model independent from:

- ERP systems
- marketplaces
- external PIM platforms
- external databases
- cloud providers
- search engines
- AI providers

---

# 3. Core Ubiquitous Language

The initial Catalog language includes:

```text
Product
SKU
Category
Brand
ProductAttribute
Specification
Media
Catalog
CatalogItem
ProductStatus
SkuStatus
ProductVariation
ProductEnrichment
```

These names represent Yunu.Commerce concepts.

External terminology must be translated before entering Catalog.

---

# 4. Product

A Product represents the conceptual commercial item.

Example:

```text
Apple iPhone 17 Pro
```

A Product is not a specific physical stock unit.

A Product may have multiple SKUs.

Example:

```text
Product
Apple iPhone 17 Pro

    ├── SKU
    │   256 GB / Black
    │
    ├── SKU
    │   512 GB / Black
    │
    └── SKU
        256 GB / Silver
```

The Product describes the shared identity and characteristics common to its variations.

---

# 5. SKU

A SKU represents a specific sellable variation of a Product.

A SKU may differ by attributes such as:

```text
Color
Size
Storage
Voltage
Pack Size
Capacity
Material
Flavor
Model
```

The actual variation dimensions depend on category.

SKU must not own:

```text
Price
Seller
Availability
Inventory
Freight
```

Those are references or concerns from other contexts.

---

# 6. Product and SKU Relationship

The initial conceptual relationship is:

```text
Product
    │
    ├── SKU
    ├── SKU
    └── SKU
```

A SKU must belong to exactly one Product.

A Product may exist temporarily without an active SKU during creation or enrichment workflows.

Whether Product and SKU belong to the same Aggregate will be explicitly decided during Aggregate modeling.

---

# 7. Product Identity

Product identity must use a domain-specific identifier.

Conceptually:

```text
ProductId
```

External product identifiers may also exist, but they must not replace the canonical Yunu.Commerce ProductId.

Examples of external identifiers:

```text
ERP Product Code
Marketplace Product Id
Legacy Product Id
PIM Id
```

These must be represented as external references.

---

# 8. SKU Identity

SKU identity must use:

```text
SkuId
```

External SKU codes may exist independently.

Examples:

```text
ERP SKU Code
Seller SKU Code
Marketplace SKU Id
EAN / GTIN
Legacy SKU Code
```

The canonical SkuId remains owned by Yunu.Commerce.

---

# 9. External References

Catalog may maintain external references for integration traceability.

Conceptual example:

```text
ExternalReference

System
Type
Value
```

Example:

```text
System = SAP
Type = Material
Value = 00000012345
```

External references must not dictate internal Domain identity.

---

# 10. Category

Category represents product classification.

Example:

```text
Electronics
    ↓
Smartphones
    ↓
Premium Smartphones
```

Category may support hierarchy.

Potential concepts:

```text
CategoryId
Name
ParentCategoryId
Status
```

Category structure belongs to Catalog.

---

# 11. Category Hierarchy

Catalog may support:

```text
Root Category
    │
    ├── Child Category
    │
    └── Child Category
```

A category may have:

```text
zero or one parent
zero or many children
```

Circular category hierarchies must not be allowed.

---

# 12. Brand

Brand represents the canonical manufacturer or commercial brand associated with a Product.

Potential concepts:

```text
BrandId
Name
Status
```

Brand must not be represented only as an arbitrary string when canonical identity is required.

---

# 13. Product Attributes

Attributes describe characteristics of a Product or SKU.

Examples:

```text
Color
Storage
Screen Size
Processor
Material
Voltage
Gender
Size
```

Attributes may be:

```text
descriptive
variation-defining
technical
searchable
filterable
```

The Domain must distinguish semantic meaning from storage representation.

---

# 14. Attribute Definition

A reusable attribute definition may contain:

```text
AttributeId
Name
DataType
Unit
AllowedValues
Required
Searchable
Filterable
VariationDefining
```

Examples of data types:

```text
Text
Number
Boolean
Date
Option
MultiOption
Measurement
```

The final model will be refined by use cases.

---

# 15. Product-Level Attributes

Some attributes apply to the Product.

Example:

```text
Brand
Product Line
Operating System
Processor Family
```

They are shared across SKUs.

---

# 16. SKU-Level Attributes

Some attributes differentiate SKUs.

Example:

```text
Color
Storage
Size
Voltage
```

Example:

```text
Product: T-Shirt

SKU A
Color = Black
Size = M

SKU B
Color = Black
Size = L
```

Variation-defining attributes must uniquely distinguish sibling SKUs where required.

---

# 17. Specifications

Specifications represent structured technical information.

Examples:

```text
Screen Resolution
Battery Capacity
Dimensions
Weight
Processor
Memory
Water Resistance
Material
```

Specifications may overlap conceptually with attributes.

A likely semantic distinction is:

```text
Attribute
used for identity, filtering or variation

Specification
used primarily for descriptive technical information
```

This distinction remains subject to refinement.

---

# 18. Media

Catalog owns descriptive product media references.

Potential media types:

```text
Image
Video
Manual
Document
360 Image
```

Media may contain:

```text
MediaId
Type
Url or ObjectReference
Position
AltText
Status
```

Binary storage itself belongs to an Infrastructure adapter.

The Domain owns media metadata and semantics.

---

# 19. Product Status

Product lifecycle must be explicit.

Potential Product statuses:

```text
Draft
PendingReview
Active
Inactive
Archived
```

Initial meanings:

- **Draft:** Product is being created or enriched.
- **PendingReview:** Product requires validation before publication.
- **Active:** Product is available for publication and downstream consumption.
- **Inactive:** Product exists but should not be actively published.
- **Archived:** Product is no longer part of the active Catalog lifecycle.

Exact transitions will be defined during Domain implementation.

---

# 20. SKU Status

SKU lifecycle may be independent from Product lifecycle.

Potential statuses:

```text
Draft
Active
Inactive
Archived
```

A Product may be Active while one SKU is inactive.

---

# 21. Product Activation

Product activation must enforce Domain invariants.

Potential future invariants may include:

```text
Product must have a valid name
Product must belong to a valid category
Product must contain required category attributes
Product must contain at least one publishable SKU
```

These rules must not be implemented until explicitly approved.

---

# 22. SKU Uniqueness

Sibling SKUs must not represent the same variation combination when those attributes define the variation.

Example:

```text
SKU A
Color = Black
Storage = 256 GB

SKU B
Color = Black
Storage = 256 GB
```

The uniqueness rule depends on configured variation attributes.

---

# 23. GTIN / EAN

SKU may contain globally recognized product identifiers such as:

```text
GTIN
EAN
UPC
ISBN
```

depending on product category.

These identifiers should be modeled explicitly when required.

They must not become the internal SkuId.

---

# 24. Catalog

Catalog may represent a curated or publishable collection of products.

Examples:

```text
Main Online Catalog
Marketplace Catalog
B2B Catalog
Regional Catalog
Seasonal Catalog
```

A Catalog references Catalog Items.

Catalog is not the same concept as Product.

---

# 25. Catalog Item

CatalogItem represents inclusion of a Product or SKU in a specific Catalog.

Potential properties:

```text
CatalogItemId
CatalogId
ProductId
SkuId
Status
PublicationData
```

The final Aggregate boundary will depend on publication requirements.

---

# 26. 1P and 3P Boundary

Catalog describes products independently from who sells them.

Therefore:

```text
Product != Offer
SKU != Seller SKU Offer
```

First-party and third-party commercial ownership belongs primarily to Sellers and Offers.

Catalog may store source/reference metadata required for traceability, but it must not own seller pricing or seller availability.

Example:

```text
Canonical Product
      │
      └── Canonical SKU
              │
              ├── 1P Offer
              │
              ├── 3P Seller A Offer
              │
              └── 3P Seller B Offer
```

The Offers Bounded Context owns those commercial relationships.

---

# 27. Pricing Boundary

Catalog must not own:

```text
Regular Price
Sale Price
Regional Price
PIX Price
Boleto Price
Installment Price
Promotional Price
```

Those concepts belong to Pricing.

Catalog identifies the Product/SKU to which pricing can refer.

---

# 28. Availability Boundary

Catalog must not own:

```text
National Availability
Regional Availability
Branch Availability
Stock Quantity
Reservation
```

Those concepts belong to Availability.

---

# 29. Fulfillment Boundary

Catalog must not own physical fulfillment topology such as:

```text
Store
Branch
Warehouse
Distribution Center
Fulfillment Node
```

Those concepts belong to Fulfillment.

---

# 30. Freight Boundary

Catalog must not calculate:

```text
Freight Price
Delivery Promise
Carrier
Delivery SLA
Regional Freight
```

Those concepts belong to Freight.

Catalog may expose physical product information required by Freight, such as dimensions and weight, through explicit contracts.

---

# 31. Search Boundary

Elasticsearch is not the Catalog source of truth.

Conceptual flow:

```text
Catalog
   │
   ▼
Integration Event
   │
   ▼
Search Projection
   │
   ▼
Elasticsearch
```

Search owns its optimized projection.

Catalog owns canonical product data.

---

# 32. AI Boundary

Generative AI may assist Catalog workflows.

Potential capabilities include:

```text
Generate product title
Generate product description
Normalize attributes
Extract specifications
Classify category
Suggest brand
Generate SEO metadata
Generate tags
Detect duplicate products
Generate embeddings
```

AI does not own the Product Aggregate.

---

# 33. AI Provider Independence

Catalog must not know whether enrichment is performed by:

```text
Azure OpenAI
Google Vertex AI
another provider
```

Provider-specific implementation belongs to AI Infrastructure.

Catalog interacts through explicit Application contracts and integration events.

---

# 34. AI Enrichment Principle

AI output must be treated as proposed data, not unquestionable business truth.

Conceptual flow:

```text
Raw Product
     │
     ▼
AI Enrichment Requested
     │
     ▼
AI Provider
     │
     ▼
Proposed Enrichment
     │
     ▼
Validation
     │
     ▼
Catalog Update
```

Catalog remains responsible for accepting valid state changes.

---

# 35. Product Enrichment

Potential enrichment information may include:

```text
GeneratedTitle
GeneratedDescription
SuggestedCategory
SuggestedAttributes
SuggestedSpecifications
SEOKeywords
Confidence
Provider
Model
GeneratedAtUtc
```

The exact model will be defined with AI use cases.

---

# 36. Canonical Product Model

Yunu.Commerce must maintain a canonical Product representation.

External systems must be translated into this model.

Conceptually:

```text
ERP Product
Marketplace Product
PIM Product
CSV Import
External API
       │
       ▼
Anti-Corruption Layer
       │
       ▼
Canonical Catalog Model
```

This protects Catalog from external schemas.

---

# 37. Import Boundary

Bulk imports belong to Application/Integration workflows.

The Domain should receive meaningful canonical commands or values rather than raw CSV, JSON, ERP or marketplace structures.

Forbidden conceptual flow:

```text
CSV DTO
   ↓
Product Aggregate directly
```

Preferred:

```text
External Data
    ↓
Adapter
    ↓
Mapping / Anti-Corruption Layer
    ↓
Application Use Case
    ↓
Domain
```

---

# 38. Domain Events

Potential Catalog Domain Events include:

```text
ProductCreatedDomainEvent
ProductUpdatedDomainEvent
ProductActivatedDomainEvent
ProductDeactivatedDomainEvent
SkuAddedDomainEvent
SkuUpdatedDomainEvent
SkuActivatedDomainEvent
SkuDeactivatedDomainEvent
```

Exact Domain Events will be created only when supported by actual business behavior.

---

# 39. Integration Events

Potential external Catalog facts include:

```text
ProductCreated
ProductUpdated
ProductActivated
ProductDeactivated
SkuCreated
SkuUpdated
SkuActivated
SkuDeactivated
```

Domain Events and Integration Events must remain distinct concepts.

---

# 40. Repository Boundary

Repository contracts belong to the inner architecture.

Potential contracts may eventually include:

```text
IProductRepository
ICategoryRepository
IBrandRepository
```

The exact repository set depends on Aggregate boundaries.

Repository interfaces must not expose:

```text
MongoDB collections
DbContext
SQL connections
Elasticsearch clients
```

---

# 41. Persistence Independence

Catalog Domain must not know whether canonical data is stored in:

```text
MongoDB
SQL Server
PostgreSQL
another database
```

Persistence is an adapter decision.

---

# 42. Cache Independence

Catalog Domain must not reference Redis.

Redis may later optimize reads or distributed technical behavior through Infrastructure.

Cache is never the owner of canonical Catalog state.

---

# 43. Aggregate Design

Aggregate boundaries will be chosen according to:

```text
transactional consistency
business invariants
concurrency
lifecycle
behavior
aggregate size
```

They must not be chosen merely because objects appear together on an API response.

---

# 44. Initial Aggregate Candidates

Potential Aggregate Roots include:

```text
Product
Category
Brand
Catalog
```

SKU may be:

```text
an Entity inside Product
```

or potentially an independent Aggregate if scale, concurrency and lifecycle requirements justify it.

This decision must be made explicitly before implementation.

---

# 45. Product Aggregate Candidate

Conceptually:

```text
Product
│
├── ProductId
├── Name
├── Brand reference
├── Category reference
├── Attributes
├── Specifications
├── Media
└── SKUs
```

This is a modeling candidate, not yet an implementation contract.

---

# 46. Value Object Candidates

Potential Catalog Value Objects include:

```text
ProductName
SkuCode
Gtin
ExternalReference
AttributeValue
Measurement
Dimensions
Weight
MediaReference
```

A type should become a Value Object when it has meaningful domain semantics and invariants.

Do not create Value Objects merely to increase DDD vocabulary.

---

# 47. Entity Candidates

Potential Entities include:

```text
SKU
Media
CatalogItem
```

Their final classification depends on identity and lifecycle requirements.

---

# 48. Domain Service Candidates

Domain Services should exist only for business operations that do not naturally belong to a single Entity or Aggregate.

Do not create generic services such as:

```text
ProductService
CatalogService
```

as containers for arbitrary logic.

Domain Services must represent explicit domain behavior.

---

# 49. Application Use Cases

Potential future Catalog use cases include:

```text
CreateProduct
UpdateProduct
ActivateProduct
DeactivateProduct

AddSku
UpdateSku
ActivateSku
DeactivateSku

CreateCategory
MoveCategory

CreateBrand

RequestProductEnrichment
ApplyProductEnrichment

PublishProduct
```

Use cases belong to Application.

Business invariants belong to Domain.

---

# 50. Commands and Queries

Catalog Application may use CQRS.

Commands change state:

```text
CreateProductCommand
UpdateProductCommand
ActivateProductCommand
AddSkuCommand
```

Queries read state:

```text
GetProductByIdQuery
GetProductBySkuQuery
SearchCatalogProductsQuery
```

CQRS does not require separate databases.

---

# 51. Read Models

Read models may differ from Domain models.

Example:

```text
ProductDetailsReadModel

ProductSummaryReadModel

ProductSkuReadModel
```

API responses must not expose Aggregate internals automatically.

---

# 52. Time

Domain timestamps must be explicit.

UTC should be used for persisted and integration timestamps.

Prefer unambiguous time representations.

Infrastructure serialization details must not dictate Domain semantics.

---

# 53. Concurrency

Product updates may require optimistic concurrency.

Potential strategies include:

```text
Version
ETag
Persistence-specific optimistic concurrency
```

The Domain model should not depend on a specific database implementation.

---

# 54. Auditability

Catalog may require audit metadata such as:

```text
CreatedAtUtc
UpdatedAtUtc
CreatedBy
UpdatedBy
```

Audit concerns should be introduced consistently without polluting Domain behavior with infrastructure-specific mechanisms.

---

# 55. Soft Delete

Products should generally not disappear silently when referenced by downstream contexts.

Lifecycle states such as:

```text
Inactive
Archived
```

are preferred over physical deletion for business records when history must be preserved.

Exact deletion rules will be defined later.

---

# 56. Validation Layers

Validation must be separated conceptually.

Application validation may verify:

```text
required request fields
input shape
basic formatting
```

Domain validation protects:

```text
business invariants
valid state transitions
entity consistency
```

Infrastructure validation protects:

```text
database constraints
external provider requirements
serialization requirements
```

---

# 57. Error Semantics

Catalog should use meaningful domain/application errors.

Examples:

```text
ProductNotFound
SkuNotFound
InvalidProductState
DuplicateSkuVariation
InvalidCategory
InvalidGtin
```

Do not expose database or vendor exceptions directly through the API.

---

# 58. Product Creation Flow

Initial conceptual flow:

```text
HTTP / Integration Input
        │
        ▼
Catalog Application
        │
        ▼
Create Product
        │
        ▼
Product Aggregate
        │
        ▼
Repository Port
        │
        ▼
Persistence Adapter
```

After persistence:

```text
Domain Event
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

---

# 59. AI-Assisted Product Creation Flow

The first GenAI vertical slice may evolve toward:

```text
Raw Product Input
       │
       ▼
Catalog Application
       │
       ▼
Create Draft Product
       │
       ▼
ProductCreated
       │
       ▼
AI Enrichment Requested
       │
       ▼
AI Module
       │
       ▼
Azure OpenAI / Google Vertex AI
       │
       ▼
ProductEnrichmentCompleted
       │
       ▼
Catalog Application
       │
       ▼
Validate Proposed Data
       │
       ▼
Update Product
```

This flow preserves Catalog ownership.

---

# 60. Search Projection Flow

After meaningful Catalog changes:

```text
ProductUpdated
      │
      ▼
Search Consumer
      │
      ▼
Search Projection
      │
      ▼
Elasticsearch
```

Search data may contain denormalized information from multiple contexts.

That does not transfer ownership to Search.

---

# 61. Integration with Offers

Catalog provides canonical Product and SKU identities.

Offers references those identities.

Conceptually:

```text
Catalog

Product
   │
   └── SKU
        │
        ▼
Offers

Offer
├── SkuId
└── SellerId
```

Catalog does not own the Offer.

---

# 62. Integration with Pricing

Pricing associates prices with the appropriate commercial identity.

Conceptually:

```text
Catalog SKU
     │
     ▼
Offer
     │
     ▼
Pricing
```

The exact pricing key strategy will be defined in the Pricing Domain.

---

# 63. Integration with Availability

Availability references canonical identifiers required to determine whether an item can be sold or fulfilled.

Catalog must not embed regional stock state inside Product or SKU.

---

# 64. Integration with Freight

Freight may require product logistics characteristics such as:

```text
Weight
Height
Width
Length
Volume
SpecialHandling
```

Catalog may own descriptive physical characteristics.

Freight owns calculation and delivery behavior.

---

# 65. Data Ownership

Catalog is the authoritative owner of:

```text
Product
SKU identity
Category
Brand
Catalog descriptive attributes
Product media metadata
```

Other contexts must not directly modify Catalog persistence.

---

# 66. No Shared Database Ownership

Even if modules initially use the same database server or cluster, tables/collections must have explicit ownership.

Forbidden:

```text
Pricing directly updating Catalog Product collection
```

Preferred:

```text
Pricing
   │
   ▼
Contract / Event / Application boundary
   │
   ▼
Catalog
```

---

# 67. Business Language over Technology Language

Domain code and documentation should use:

```text
Product
SKU
Category
Brand
ActivateProduct
AddSku
```

rather than:

```text
MongoDocument
ElasticProduct
KafkaProduct
ProductDTOEntity
```

Technology vocabulary belongs outside the Domain.

---

# 68. Domain Purity

Catalog Domain must not reference:

```text
ASP.NET Core
Entity Framework
MongoDB.Driver
Kafka
Redis
Elasticsearch
Azure SDK
Google Cloud SDK
OpenTelemetry
HTTP clients
```

The Domain must remain executable and testable without infrastructure.

---

# 69. Testing Strategy

Catalog Domain tests should focus on:

```text
business behavior
state transitions
invariants
SKU uniqueness
attribute rules
category rules
value objects
domain events
```

Mocks should not be required for pure Aggregate behavior.

---

# 70. Architecture Questions Before Implementation

Before implementing Product and SKU, explicitly decide:

```text
Is Product the Aggregate Root for SKU?

Can Product exist without a SKU?

Can SKU lifecycle be changed independently?

How many SKUs can a Product realistically contain?

What defines SKU uniqueness?

Are Brand and Category independent Aggregates?

Which attributes belong to Product vs SKU?

How are category-specific required attributes defined?

What makes a Product publishable?

What requires human approval after AI enrichment?

How are external product identities mapped?

What concurrency guarantees are required?
```

These decisions must be made from business requirements rather than framework convenience.

---

# 71. Initial Implementation Scope

The first Catalog implementation should remain intentionally small.

Recommended first slice:

```text
Product
SKU
ProductId
SkuId
ProductName
ProductStatus
SkuStatus
basic Product creation
basic SKU creation
Domain Events
repository port
unit tests
```

Do not implement the entire future Catalog model in the first iteration.

---

# 72. Evolution Principle

The Catalog model will evolve as real use cases are implemented.

Avoid designing every possible e-commerce feature upfront.

The architecture must protect the Domain while allowing incremental refinement.

---

# 73. Core Rule

Catalog owns the canonical answer to:

> What is the product?

Offers answers:

> Who sells it?

Pricing answers:

> For how much and under which payment condition?

Availability answers:

> Where and in what quantity can it be sold?

Fulfillment answers:

> From where can it be fulfilled?

Freight answers:

> How can it be delivered and at what cost?

Search answers:

> How can customers efficiently discover it?

AI answers:

> How can intelligence assist enrichment, understanding and interaction?

These boundaries must remain explicit.

---

# 74. Final Principle

The Catalog Domain is the canonical semantic foundation of Yunu.Commerce product information.

It must remain:

```text
business-focused
provider-independent
database-independent
search-independent
AI-provider-independent
seller-independent
pricing-independent
availability-independent
freight-independent
```

Infrastructure may change.

External systems may change.

AI providers may change.

The Catalog business model must remain protected.
