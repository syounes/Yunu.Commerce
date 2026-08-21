# ADR-0013: Configurable Canonical Taxonomy Root Topology

- **Status:** Accepted
- **Date:** 2026-08-21
- **Decision Owners:** Yunu.Commerce Architecture
- **Scope:** Catalog Bounded Context — Canonical Taxonomy governance

## 1. Context

`CanonicalTaxonomyNode` (`Catalog.Domain.CanonicalTaxonomy`) structurally
supports any number of independent root nodes. A node is a root when:

```
ParentId == null
```

There is no global Domain or database invariant stating "there can only ever
be one root": `CanonicalTaxonomyNode`, `ICanonicalTaxonomyRepository` and the
SQL Server schema (`Catalog.CanonicalTaxonomyNodes`,
deploy/databases/sqlserver/009-reset-canonical-taxonomy-starter.sql) remain
unaware of any root-count policy and continue to allow `ParentId IS NULL` on
any number of rows.

This is intentional. Different catalog deployments can legitimately require:

- one technical/global root (the current Yunu catalog profile);
- several departmental roots;
- multiple independent trees;
- imported/migrated structures that temporarily contain multiple roots
  before being consolidated.

Root topology expectations are therefore a matter of **customer/governance
policy**, not a structural capability limit of `CanonicalTaxonomyNode`. This
distinction must be documented explicitly so that future work does not
conflate "the current Yunu profile expects one root" with "Canonical
Taxonomy can only ever have one root".

## 2. Decision

The Catalog Application layer defines a Root Topology Policy
(`Catalog.Application.CanonicalTaxonomy.RootTopology`), independent from the
`CanonicalTaxonomyNode` Aggregate:

`CanonicalTaxonomyRootMode`:

```
SingleRoot
MultipleRoots
```

`CanonicalTaxonomyRootPolicyOptions`, bound from
`"Catalog:CanonicalTaxonomy:RootTopology"`:

```
RootMode
PrimaryRootCode
PrimaryRootName
```

The current Yunu catalog profile configures:

```
RootMode = SingleRoot
PrimaryRootCode = "catalog"
PrimaryRootName = "Catalog"
```

`PrimaryRootName` is descriptive/configuration metadata only. The policy
never rewrites or normalizes an existing persisted bootstrap root's `Name`
merely because a different `PrimaryRootName` is configured: the policy
evaluates observed topology, it does not mutate `CanonicalTaxonomy`.

### 2.1 Single Root Semantics

When `RootMode = SingleRoot`, `PrimaryRootCode` and `PrimaryRootName` are
required (`CanonicalTaxonomyRootPolicyOptionsValidator` enforces this at
startup). `ICanonicalTaxonomyRootTopologyAuditor`
(`CanonicalTaxonomyRootTopologyAuditor`) evaluates an observed collection of
root nodes and reports:

- `NoRootFound` — no root node exists;
- `MultipleRootsFoundForSingleRootPolicy` — more than one root node exists;
- `ConfiguredPrimaryRootNotFound` — exactly one root exists, but its `Code`
  does not match the configured `PrimaryRootCode`.

Stable logical root identity is based on `Code`, not on numeric database
identity (`CanonicalTaxonomyNodeId`, a persistence detail) and not on `Path`
(mutable/derived structure recomputed on rename, see
`CanonicalTaxonomyNode.Update`).

### 2.2 Multiple Roots Semantics

When `RootMode = MultipleRoots`, any number of `ParentId == null` nodes is
valid; the auditor accepts the observed roots unconditionally (subject only
to the "roots have no parent" precondition it checks regardless of mode). The
Domain and SQL Server schema already permit this and are not changed by this
ADR. `PrimaryRootCode`/`PrimaryRootName` may be left unset in this mode.

The following are explicitly rejected as future implementation choices for
this policy:

- a filtered `UNIQUE` index on `ParentId IS NULL`;
- a singleton-root repository invariant;
- a global "only one root" Aggregate rule inside `CanonicalTaxonomyNode`.

A configured primary root may still be useful under `MultipleRoots` for
customer policy, navigation, or audit context, but it never invalidates the
existence of other roots.

### 2.3 Policy Boundary

`ICanonicalTaxonomyRootTopologyAuditor` belongs to `Catalog.Application`
governance/configuration, not to `CanonicalTaxonomyNode` Aggregate structural
invariants. The auditor is:

- deterministic;
- side-effect-free;
- non-AI;
- read/evaluate only.

It must never create roots, rename roots, move nodes, merge trees, archive
nodes, or otherwise repair topology automatically. Future AI/audit workflows
may consume its result as one input among others, but AI does not define the
topology rule itself; the rule is owned by
`CanonicalTaxonomyRootPolicyOptions`.

## 3. Relation to Source Taxonomy

The Root Topology Policy described here applies exclusively to the desired
Yunu/customer **Canonical** Taxonomy topology. It does not constrain the
topology of external source taxonomies (see
docs/adr/0014-provider-neutral-source-taxonomy.md, planned): an upstream
taxonomy may legitimately contain one root, multiple roots, or a
provider-specific tree shape, and any future normalized `SourceTaxonomy`
representation must preserve that upstream topology faithfully rather than
having it filtered through this policy.

A future `CanonicalTaxonomyResolve` capability may consider a source
taxonomy candidate together with a customer audit profile, this Root
Topology Policy, and the existing `CanonicalTaxonomy` when producing
proposals — but that resolution/proposal flow is not implemented by this
ADR. Source taxonomy roots and Canonical Taxonomy policy are not the same
concept and must not be conflated.

## 4. Consequences

**Positive:**

- Supports different customer taxonomy strategies (single technical root vs.
  multiple independent trees) without any Domain or schema change.
- Avoids hard-coding Yunu's current `SingleRoot` preference as a universal
  structural invariant.
- Preserves multiple-root imports/migrations as a structurally valid,
  auditable state instead of an error condition baked into the schema.
- Makes topology expectations explicit and testable
  (`CanonicalTaxonomyRootTopologyAuditorTests`,
  `CanonicalTaxonomyRootPolicyOptionsValidatorTests`).
- Keeps `CanonicalTaxonomyNode` structural rules independent from customer
  governance concerns.

**Trade-offs:**

- Topology validity becomes configuration-dependent: the same observed data
  can be Valid or Invalid depending on `CanonicalTaxonomyRootPolicyOptions`.
- Consumers requiring a primary root must use the policy/configuration
  (`PrimaryRootCode`) rather than assuming a single root always exists.
- Deployments configured with `MultipleRoots` require every consumer to
  avoid implicit singleton-root assumptions (e.g. "the first/only root").

## 5. Explicitly Not Changed

- No SQL schema change is introduced by this ADR.
- No unique-root constraint is added to `Catalog.CanonicalTaxonomyNodes`.
- `CanonicalTaxonomyNode` structure/behavior is unchanged.
- Multiple roots remain structurally supported by the Domain and by SQL
  Server regardless of the configured `CanonicalTaxonomyRootMode`.
- No AI implementation is introduced.
- No `SourceTaxonomy` implementation is introduced (see ADR-0014).
- No automatic topology repair is introduced.
- No public mutation API is introduced.

## 6. Related Decisions

- docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md
- docs/adr/0011-no-public-crud-for-structural-catalog-data.md
- docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md
- docs/adr/0014-provider-neutral-source-taxonomy.md
