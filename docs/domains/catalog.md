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

# 11.1. Product Classification Model (Implemented)

This section documents the currently implemented Product classification model,
which supersedes any earlier implication that Product directly owns a
`CategoryId`.

Product does **not** reference `CategoryId`, `SubCategoryId` or `DepartmentId`
directly. The implemented Product Aggregate exposes:

````````markdown
Product does not store any internal Family hierarchy reference.

`GoogleCategory` is a denormalized reference (`GoogleCategoryReference.Id` +
`GoogleCategoryReference.Path`) resolved by Catalog.Application from the
canonical Google Product Taxonomy (SQL Server `GoogleTaxonomyCategories`,
implemented behind `IGoogleTaxonomyRepository`) **before** the Product
Aggregate is constructed. Only active, leaf categories may be used. The Product
Domain never performs this lookup itself.

---
````````

# 12. Brand

Brand represents the canonical manufacturer or commercial brand associated with a Product.

Potential concepts:

```text
BrandId
Name
```

Brand information may be managed externally, but the reference must be maintained in Catalog.

Brand must not be represented only as an arbitrary string when canonical identity is required.

---

# 13. Product Attribute

Product attributes represent specific characteristics or features of a Product.

Example:

```text
Color: Red
Memory: 128GB
```

Attribute types and management are not owned by Catalog.

## 13.1. SKU Attribute Foundation (Implemented)

SKU owns its assigned attribute values as part of its own Aggregate boundary
(docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md). Product does
not own, construct or persist Sku attributes.

Attribute reference data — definitions, options and Google category attribute
rules — is owned by SQL Server (`Catalog.AttributeDefinitions`,
`Catalog.AttributeOptions`, `Catalog.GoogleCategoryAttributeRules`,
deploy/databases/sqlserver/002_create_sku_attribute_catalog.sql). Catalog.Domain never queries
SQL Server directly: Catalog.Application resolves and validates attribute
definitions/options via `IAttributeCatalogRepository` before asking the Sku
Aggregate (`Sku.AssignAttribute` / `ReplaceAttribute` / `RemoveAttribute`) to
assign a validated `SkuAttribute`.

MongoDB owns the transactional Sku Aggregate, including its complete
attributes collection, persisted atomically with the rest of the Sku document
(`SkuDocument.Attributes`, `skus` collection). Legacy Sku documents without an
`Attributes` field hydrate as an empty attributes collection; no destructive
migration is required.

`Catalog.SkuAttributeValues` (SQL Server) is **not** written by this use case.
It is reserved for a possible future relational projection/read model and must
not be treated as the transactional source of truth for Sku attributes.

AI/LLM-based interpretation of natural-language attribute values (automatic
extraction, embeddings, semantic search, pgvector synchronization) is
explicitly deferred; this foundation only supports explicit, structured
attribute assignments supplied by a caller.

---

# 14. Product Media

Product media includes images, videos, and other materials that represent the Product.

Media examples:

```text
Image: front_view.jpg
Video: promo_video.mp4
```

Media management and storage are not owned by Catalog.

---

# 15. Technical Specification

Technical specifications provide detailed information on product characteristics.

Example:

```text
Voltage: 220V
Frequency: 50Hz
```

Specification management is not owned by Catalog.

---

# 16. Product Lifecycle

Product lifecycle defines the stages a product goes through from introduction to retirement.

Lifecycle stages:

```text
Active
Inactive
```

Lifecycle management is not owned by Catalog.

---

# 17. Catalog Publication State

Catalog publication state indicates the visibility and availability of a product in the catalog.

States:

```text
Draft
Published
Archived
```

Publication state management is not owned by Catalog.

---

# 18. Product Descriptive Metadata

Descriptive metadata provides additional information about a product for indexing and search.

Example:

```text
MetaTitle: Buy Apple iPhone 17 Pro
MetaDescription: Latest iPhone with advanced features
```

Metadata management is not owned by Catalog.

---

# 19. AI Enrichment Approval State

AI enrichment approval state indicates if AI-generated content for a product has been approved.

States:

```text
Pending
Approved
Rejected
```

AI content approval processes are not owned by Catalog.

---

# 20. Integration Events

Integration events are emitted by the Catalog Domain to notify other systems of significant changes.

Examples:

```text
ProductCreated
ProductUpdated
ProductDeleted
```

Event details and handling are not owned by Catalog.
