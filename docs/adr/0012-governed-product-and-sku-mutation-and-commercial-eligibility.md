# ADR-0012: Governed Product and Sku Mutation and Commercial Eligibility

- **Status:** Accepted
- **Date:** 2026-08-20
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

`SkuStatus` keeps the same shape as `ProductStatus`; a Draft Sku has never
been operational, so it is never "deactivated" (`Draft -> Inactive` does not
exist):

```
Draft -> Active | Archived
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
`Catalog.Application` through the atomic coordination boundary described in
section 2.5, `IProductSkuConcurrencyCoordinator`
(`MongoProductSkuConcurrencyCoordinator` for MongoDB):

- **Archiving a Product** (`TransitionProductStatusHandler`) delegates to
  `IProductSkuConcurrencyCoordinator.ArchiveProductAsync(...)`. The
  coordinator performs the required Product/Sku checks atomically within the
  MongoDB transactional coordination boundary; the handler translates the
  returned coordination result into the appropriate Application exception
  (e.g. `ProductHasNonArchivedSkusException`). `ISkuRepository.ExistsNonArchivedByProductIdAsync`
  is not the handler's final concurrency mechanism.
- **A Sku transitioning to a non-Archived status while its owning Product
  may be `Archived`** (`TransitionSkuStatusHandler`) delegates to
  `IProductSkuConcurrencyCoordinator.TransitionSkuIfProductNotArchivedAsync(...)`.
  The Product status check occurs as part of the coordinator's transactional
  operation; the handler translates the coordination result into the
  appropriate Application exception (e.g. `ProductArchivedException`). A
  direct `IProductRepository.GetByIdAsync` read is not the final race-safe
  mechanism. A Sku may still transition *to* `Archived` regardless of its
  Product's status.
- **Creating a Sku** (`CreateSkuHandler`) delegates to
  `IProductSkuConcurrencyCoordinator.CreateSkuIfProductNotArchivedAsync(...)`,
  preventing a non-Archived Sku from being created concurrently with Product
  archive.

These facts remain unchanged: `Product.Status == Archived` implies no Sku
belonging to that Product may be non-Archived; Product and Sku remain
independent Aggregate Roots; cross-aggregate guards live outside the
Aggregates; a Sku may transition to `Archived` even when its Product is
`Archived`; Product status is never propagated into Sku and Sku status is
never propagated into Product; docs/adr/0010 remains unchanged.

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

Instead of generic mutation/update or a single generic "set Status to X"
endpoint, semantic lifecycle commands are exposed per Aggregate so HTTP can
never send an arbitrary target Status:

- `POST /api/catalog/products/{productId}/deactivate` — Active -> Inactive.
- `POST /api/catalog/products/{productId}/reactivate` — Inactive -> Active.
- `POST /api/catalog/products/{productId}/archive` — any non-terminal state -> Archived.
- `POST /api/catalog/skus/{skuId}/deactivate` — Active -> Inactive.
- `POST /api/catalog/skus/{skuId}/reactivate` — Inactive -> Active.
- `POST /api/catalog/skus/{skuId}/archive` — any non-terminal state -> Archived.

No `PUT`/`PATCH` structural-mutation endpoint is introduced for either
Aggregate. The initial `Draft -> Active` transition is intentionally **not**
exposed by any public endpoint for either Aggregate: it remains
internal/governed, reserved for the (not-yet-implemented) ProductProposal
materialization flow. "Reactivate" therefore only ever means
`Inactive -> Active`, never `Draft -> Active`.

### 2.5 Concurrency

Product and Sku remain independent Aggregate Roots, each with its own
repository (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md);
this decision does not merge them into one Aggregate and does not establish
a general transactional boundary between them. A narrow
`IProductSkuConcurrencyCoordinator` port
(`Catalog.Domain.Concurrency`) exists exclusively to atomically enforce the
one invariant that spans both Aggregates:

```
Product.Status == Archived
    =>
no Sku belonging to that Product has a Status other than Archived.
```

Without coordination, "read Skus, then write Product" (Archive) and "read
Product, then write Sku" (CreateSku / reactivate / block) can each pass their
own guard check against a state that changes before the other operation
commits (write skew). `MongoProductSkuConcurrencyCoordinator` is the MongoDB
adapter for this port. For each of the three operations it protects
(`ArchiveProductAsync`, `CreateSkuIfProductNotArchivedAsync`,
`TransitionSkuIfProductNotArchivedAsync`), it opens a single MongoDB
multi-document transaction (`session.WithTransactionAsync`) and, inside that
transaction, conditionally increments the same field on the same Product
document: `ProductDocument.LifecycleRevision`, an infrastructure-only token
never mapped onto the Domain `Product` Aggregate. Because every competing
operation touches the same `LifecycleRevision` field on the same Product
document, concurrent transactions cannot silently commit a write-skew state.
The losing transaction either fails its conditional write against stale
state or is transparently retried by the MongoDB driver (its built-in
transient-transaction-error retry for the underlying "WriteConflict" case)
and then observes the newly committed state, returning the corresponding
coordination result such as `ConcurrencyConflict`, `ProductArchived`, or
`NonArchivedSkuExists`, instead of silently interleaving.
`TransitionProductStatusHandler` and `TransitionSkuStatusHandler` translate
a losing result directly into an exception (first-writer-wins); neither
handler reloads the Aggregate and
retries the original command against newer state.

This is a conditional-write coordination mechanism scoped to one
cross-aggregate invariant, not a distributed lock. No application-level
command retry loop exists; the only retry behavior involved is MongoDB
driver's own built-in transient-transaction-error retry inside
`WithTransactionAsync` for the underlying "WriteConflict" case, which is
transparent to callers of this port.

Because standalone (non-replica-set) MongoDB does not support multi-document
transactions, this design requires MongoDB configured as a replica set — a
single-node replica set is sufficient for local/dev/test
(deploy/docker/docker-compose.yml documents and health-checks this
requirement).

Non-Archive Product transitions and Sku transitions *to* `Archived` have no
cross-aggregate concern (an Archived Product's Skus may still individually
become Archived) and are persisted directly through
`IProductRepository.UpdateStatusAsync` / `ISkuRepository.UpdateStatusAsync`,
an atomic conditional update (`{ Id, Status = expectedCurrentStatus } ->
Set(Status = newStatus)`) that still returns `false` — surfaced as a
concurrency-conflict exception, not retried — when another writer already
changed the document.

## 3. Consequences

**Positive:**

- A single, explicit, testable state machine per Aggregate replaces ad hoc
  status assignment.
- Cross-aggregate rules are visible in Application handlers and the
  coordinator port, not hidden inside either Aggregate, preserving Aggregate
  independence (docs/adr/0010 unchanged).
- Commercial Eligibility has one authoritative definition and is guaranteed
  never to drift into persisted state.
- Public API surface for Product/Sku mutation is minimal and semantic
  (deactivate/reactivate/archive), consistent with docs/adr/0011's
  read-only/governed-mutation philosophy.
- The `LifecycleRevision`-based transactional coordination gives the
  cross-aggregate invariant a genuine common write point, closing the write
  skew that a purely optimistic per-document conditional update cannot
  prevent on its own.

**Trade-offs:**

- The coordinator requires MongoDB configured as a replica set (even a
  single-node one) in every environment, including local/dev/test, because
  standalone MongoDB does not support the transactions it relies on.
- `ProductProposal` → `Product`/`Sku` conversion (the intended caller of
  `CreateProductHandler`/`CreateSkuHandler`) is out of scope for this ADR and
  remains a follow-up.

## 4. Explicitly Not Changed

- SQL Server schemas/scripts: untouched.
- PostgreSQL/pgvector: untouched.
- `ProductProposal` Aggregate/status/boundary: untouched.
- docs/adr/0010 and docs/adr/0011: preserved unchanged.
