# ADR-0010: Separate Product and Sku Aggregate Boundaries

- **Status:** Accepted
- **Date:** 2026-08-12
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Catalog Bounded Context — Product and Sku Aggregate boundaries and persistence

## 1. Context

The initial Catalog Domain slice modeled `Sku` as an Entity owned by the `Product`
Aggregate Root:

`Sku` instances could only be constructed through `Product.AddSku(...)`, had no
independent identity boundary from a persistence perspective, and were stored
embedded inside the `ProductDocument` in MongoDB (`products` collection, `Skus[]`
array). There was no `ISkuRepository` and no standalone `skus` collection.

This was a valid, documented first-cut DDD decision for the earliest Catalog
slice (see the original `Product.cs` remarks), but it does not fit the target
architecture for Yunu.Commerce, where SKU is expected to be operated on
independently by multiple future capabilities.

## 2. Decision

`Sku` is promoted to an **independent Aggregate Root** with its own identity,
lifecycle, domain events and persistence boundary.

`Sku` no longer belongs to the `Product` Aggregate's consistency boundary.
`Product` no longer exposes `Skus` or `AddSku(...)`.

Both Aggregates remain in the same Catalog Bounded Context — no new Bounded
Context is created for Sku.

## 3. Previous Model

````````

Persistence:

````````


# Response

````````

## 4. New Model

````````


# Response

Persistence:

````````


# Response

````````

`IProductRepository` and `ISkuRepository` are both minimal Domain ports,
following the same shape and constraints already enforced by
`RepositoryPortRuleTests` for `IProductRepository` (no vendor types, no
generic repository abstraction, no Update/Delete/Search operations beyond
what current use cases require).

## 5. Why We Changed It

Yunu.Commerce's roadmap includes independent operations targeting SKU that do
not naturally belong to Product as the transactional owner:

````````

Modeling Sku as an owned Entity inside Product would force every one of these
operations to load, mutate and re-persist the entire Product Aggregate (or
otherwise bypass the Aggregate boundary), which:

- creates unnecessary transactional coupling between unrelated concerns
  (e.g., a Pricing-triggered SKU status change should not require loading or
  re-saving unrelated Product descriptive data),
- makes independent SKU lifecycle operations (Activate/Block/Discontinue)
  awkward to express and test in isolation,
- does not scale well once SKU is referenced by Offers, Pricing, Availability
  and Search read models, all of which care about SKU identity and lifecycle
  without needing the full Product Aggregate.

## 6. Trade-offs

**Gained:**

- SKU can be created, queried and transitioned independently through its own
  Application use cases (`CreateSku`, `GetSkuById`, `GetSkusByProductId`) and
  repository (`ISkuRepository` / `MongoSkuRepository`).
- Future capabilities (GenAI, Pricing, Promotion, Availability, Search) can
  depend on SKU without depending on Product's full Aggregate boundary.
- Persistence is no longer coupled: adding/removing SKUs does not require
  rewriting the `ProductDocument`.

**Lost / accepted:**

- No cross-Aggregate transactional guarantee between a Product and its Skus
  at write time (e.g., creating a Sku for a non-existent Product is not
  currently validated by the Domain — deferred until a use case requires it).
- The API's `GetProductById` response, which still returns Product + Skus
  together, now requires composing two repository calls at the Application
  layer instead of a single Aggregate load. This is intentional: composition
  for read purposes belongs to the Application/read-model layer, not to the
  Product Aggregate (docs/architecture "CQRS" guidance).

## 7. Why This Change Is Acceptable Now

Yunu.Commerce is still in its initial development phase. There is no
production data, and the only persisted Catalog data exists in local/test
MongoDB instances. This allows us to correct the Aggregate boundary now,
before Offers, Pricing, Availability, Promotion or GenAI code depends on the
previous (embedded) SKU shape.

No migration tooling is introduced for this change: local Mongo data may be
discarded and recreated, and integration tests are updated to target the new
`skus` collection directly.

## 8. Consequences

- `Product.Skus` and `Product.AddSku(...)` are removed.
- `SkuAddedDomainEvent` (raised only by the old `Product.AddSku`) is no longer
  raised; Sku lifecycle events (`SkuCreatedDomainEvent`, `SkuActivatedDomainEvent`,
  `SkuBlockedDomainEvent`, `SkuDiscontinuedDomainEvent`) are raised by the Sku
  Aggregate itself.
- `ProductStatus` and `SkuStatus` enum values are **not** renamed in this
  change; lifecycle semantics review (e.g., aligning names like "Blocked" or
  "Discontinued" with enum values) is deferred to a separate decision.
- `GetProductByIdHandler` now composes `IProductRepository.GetByIdAsync` and
  `ISkuRepository.GetByProductIdAsync` to preserve the existing
  `ProductResponse` API contract.
````````


# Response
````````markdown
