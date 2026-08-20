# ADR-0012: Governed Product and Sku Mutation and Commercial Eligibility

- **Status:** Accepted
- **Date:** 2026-09-10
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Catalog Bounded Context — `Product`, `Sku`

## 1. Context

`Product` and `Sku` are independent Aggregate Roots
(docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md). Prior to
this decision:

- `ProductStatus` included a `PendingReview` value that was never actually
  used by any lifecycle transition; human review of catalog input already
  belongs to `ProductProposal`/`ProductProposalStatus`, a separate boundary
  (docs task: "Catalog intent resolution orchestration").
- `Product` had no enforced lifecycle state machine; its `Status` could be
  set to any value at creation with no transition guard.
- `Sku.Activate()/Block()/Discontinue()` were permissive: an `Archived` Sku
  could be reactivated, and there was no single source of truth for which
  transitions are valid.
- Both Aggregates were directly creatable via public `POST` endpoints
  (`POST /api/catalog/products`, `POST /api/catalog/products/{productId}/skus`),
  bypassing the ProductProposal governance flow described for structural
  Catalog data in docs/adr/0011.
- No commercial-eligibility concept existed; nothing prevented an inactive
  Product from exposing an active-looking Sku, or vice versa, in read models.

## 2. Decision

### 2.1 Lifecycle

`ProductStatus.PendingReview` is removed. A materialized `Product` only has:

```
Draft -> Active | Archived
Active -> Inactive | Archived
Inactive -> Active | Archived
Archived -> (terminal)
```

`SkuStatus` keeps the same shape, with `Draft` additionally allowed to move
directly to `Inactive`:

```
Draft -> Active | Inactive | Archived
Active -> Inactive | Archived
Inactive -> Active | Archived
Archived -> (terminal)
```

Both state machines are enforced exclusively inside the respective Aggregate
(`Product.TransitionTo`, `Sku.Activate/Block/Discontinue`), mirroring the
existing `CanonicalTaxonomyNode.TransitionTo` pattern. Invalid transitions
throw `InvalidProductStatusTransitionException` /
`InvalidSkuStatusTransitionException`. Product status is never propagated to
Sku, and Sku status is never propagated to Product: each Aggregate's
lifecycle remains fully independent, preserving docs/adr/0010 unchanged.

### 2.2 Cross-aggregate guards (Application layer)

Because Product and Sku are independent Aggregates, cross-aggregate rules
cannot live inside either Aggregate. They are enforced by
`Catalog.Application`:

- **Archiving a Product** (`TransitionProductStatusHandler`) is blocked while
  at least one of its Skus is not `Archived`
  (`ISkuRepository.ExistsNonArchivedByProductIdAsync`), raising
  `ProductHasNonArchivedSkusException`.
- **A Sku leaving `Archived`, or being (re)activated/blocked**, is blocked
  while its owning Product is `Archived` (`TransitionSkuStatusHandler`
  checks `IProductRepository.GetByIdAsync`), raising
  `ProductArchivedException`. A Sku may still transition *to* `Archived`
  regardless of its Product's status.
- **Creating a Sku** under an `Archived` Product is blocked by
  `CreateSkuHandler`.

### 2.3 Commercial Eligibility

Commercial Eligibility is a derived, read-only concept:

```
CommerciallyEligible = Product.Status == Active && Sku.Status == Active
```

It is computed by `CommercialEligibilityPolicy.IsEligible` (Application
layer) exclusively inside read models
(`GetProductByIdHandler`, `GetSkuByIdHandler`,
`GetSkusByProductIdHandler`). It is **never persisted**: no
`CommerciallyEligible` field exists on `ProductDocument` or `SkuDocument`,
and no Aggregate stores it. It is composed at read time, not inherited by
either Aggregate.

### 2.4 API Governance

Direct public creation of `Product` and `Sku` is removed:

- `POST /api/catalog/products` is removed.
- `POST /api/catalog/products/{productId}/skus` is removed.

`CreateProductHandler` and `CreateSkuHandler` remain as internal Application
services, intended for the governed ProductProposal conversion flow (not
implemented by this ADR), consistent with the structural-data governance
model in docs/adr/0011.

Two new semantic lifecycle endpoints are added instead of generic
mutation/update endpoints:

- `POST /api/catalog/products/{productId}/status` — body `{ "status": "..." }`.
- `POST /api/catalog/skus/{skuId}/status` — body `{ "status": "..." }`.

No `PUT`/`PATCH` structural-mutation endpoint is introduced for either
Aggregate.

### 2.5 Concurrency

`IProductRepository.UpdateStatusAsync` and `ISkuRepository.UpdateStatusAsync`
apply an atomic, conditional MongoDB update
(`{ Id, Status = expectedCurrentStatus } -> Set(Status = newStatus)`),
returning `false` when no document matched (already-changed or missing).
`TransitionProductStatusHandler`/`TransitionSkuStatusHandler` reload the
Aggregate and retry (bounded, 3 attempts) on a `false` result, avoiding a
lost-update race without introducing a distributed lock or a version field.

## 3. Consequences

**Positive:**

- A single, explicit, testable state machine per Aggregate replaces ad hoc
  status assignment.
- Cross-aggregate rules are visible in Application handlers, not hidden
  inside either Aggregate, preserving Aggregate independence.
- Commercial Eligibility has one authoritative definition and is guaranteed
  never to drift into persisted state.
- Public API surface for Product/Sku mutation is minimal and semantic
  (`.../status`), consistent with docs/adr/0011's read-only/governed-mutation
  philosophy.

**Trade-offs:**

- The bounded retry loop on `UpdateStatusAsync` is a pragmatic optimistic-
  concurrency strategy; it does not fully eliminate the race under very high
  contention (acceptable at this phase; no distributed lock is introduced).
- `ProductProposal` → `Product`/`Sku` conversion (the intended caller of
  `CreateProductHandler`/`CreateSkuHandler`) is out of scope for this ADR and
  remains a follow-up.

## 4. Explicitly Not Changed

- SQL Server schemas/scripts: untouched.
- PostgreSQL/pgvector: untouched.
- `ProductProposal` Aggregate/status/boundary: untouched.
- docs/adr/0010 and docs/adr/0011: preserved unchanged.
