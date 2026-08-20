# ADR-0011: No Public CRUD Mutation Endpoints for Structural Catalog Data

- **Status:** Accepted
- **Date:** 2026-08-20
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Catalog Bounded Context — `CanonicalTaxonomy`, `SegmentDefinition`, `SegmentOption`

## 1. Context

`CanonicalTaxonomy`, `SegmentDefinition` and `SegmentOption` are structural
catalog data: they define the taxonomy tree and the segmentation vocabulary
that `Product` and `Sku` Aggregates reference (via Canonical Taxonomy node
assignment and `SegmentAssignment`s). Unlike `Product`/`Sku` instance data,
these structures change infrequently, are foundational to search, pricing,
availability and AI enrichment, and are expected to be curated (e.g. by
catalog governance tooling, imports, or administrative processes) rather than
mutated ad hoc through a public HTTP surface.

Exposing public `POST`/`PUT`/`PATCH`/`DELETE` endpoints for these resources
would allow uncontrolled structural changes (renames, status transitions,
deletions) that can silently break Product/Sku segment assignments, Canonical
Taxonomy associations, and downstream Search/AI projections, bypassing the
usage guards implemented in the Application layer (see
`ISegmentDefinitionUsageReader`, `IProductRepository`/`ISkuRepository` usage
checks in `UpdateSegmentDefinitionHandler`, `CreateSegmentOptionHandler`, and
`UpdateSegmentOptionHandler`).

## 2. Decision

`CatalogCanonicalTaxonomyEndpoints` and `CatalogSegmentEndpoints` expose
**read-only (`GET`) routes only**.

No public HTTP endpoint maps `POST`, `PUT`, `PATCH`, or `DELETE` for:

- `CanonicalTaxonomy` (nodes, tree structure)
- `SegmentDefinition`
- `SegmentOption`

The corresponding Application layer Commands and Handlers
(`CreateSegmentDefinitionHandler`, `UpdateSegmentDefinitionHandler`,
`CreateSegmentOptionHandler`, `UpdateSegmentOptionHandler`, and their
Canonical Taxonomy equivalents) remain available for internal/administrative
composition (e.g. import tooling, an internal admin host, or a future
governance workflow), but they are intentionally **not wired to the public
API host**.

Lifecycle and usage guards implemented in the Application layer (blocking
`Archived` transitions and Segment Option creation under an archived parent
while the structure is still in effective use) remain the enforcement
mechanism for whichever caller does invoke these handlers.

## 3. Consequences

**Positive:**

- Prevents uncontrolled structural mutation of shared catalog vocabulary
  through the public API.
- Keeps usage/lifecycle guards as the single enforcement point regardless of
  which internal caller invokes the handlers.
- Public API surface for these resources remains simple and cache-friendly
  (read-only).

**Negative / Trade-offs:**

- Any future need for administrative mutation of these structures requires a
  separate, explicitly gated surface (internal host, tooling, or a
  role-restricted admin API) rather than the public Catalog API.
- Existing Application handlers must be reused carefully to avoid
  accidentally exposing them through a future public endpoint without
  revisiting this decision.

## 4. Alternatives Considered

- **Expose public CRUD endpoints protected by authorization policies.**
  Rejected for this phase: authorization alone does not address the blast
  radius of structural changes across Product/Sku/Search/AI, and the current
  project phase does not yet define an administrative identity/authorization
  model.
- **Soft-delete/versioning support at the API level.** Deferred; not required
  while no public mutation surface exists.

## 5. Related Decisions

- ADR-0001 — DDD, Clean Architecture, Hexagonal Architecture
- ADR-0010 — Separate Product and Sku Aggregate Boundaries
