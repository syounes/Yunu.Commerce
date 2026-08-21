namespace Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

/// <summary>
/// Outcome of an attempt to create a child node, coordinated with its
/// parent's own concurrency Revision (docs task: "Yunu.Commerce - Canonical
/// Taxonomy Concurrency Guard"). Child creation participates in the
/// parent's Revision so the Archive x CreateChild structural race cannot
/// commit an Archived parent with a newly-created child: whichever writer
/// commits first invalidates the other's expected parent Revision.
/// </summary>
public enum AddCanonicalTaxonomyChildOutcome
{
    Created,
    ParentNotFound,
    ParentArchived,
    ParentConcurrencyConflict
}

/// <summary>
/// Result of <see cref="ICanonicalTaxonomyRepository.AddChildAsync"/>.
/// </summary>
public sealed class AddCanonicalTaxonomyChildResult
{
    public required AddCanonicalTaxonomyChildOutcome Outcome { get; init; }

    public CanonicalTaxonomyNodeId? AssignedId { get; init; }
}

/// <summary>
/// Persistence port for the CanonicalTaxonomyNode Aggregate Root (docs task:
/// "Canonical Taxonomy + Segments Domain" §4, §19-§22). Backed by SQL Server
/// (Catalog.CanonicalTaxonomyNodes,
/// deploy/databases/sqlserver/006-create-canonical-taxonomy-segmentation.sql),
/// which remains the source of truth. Catalog.Domain never queries SQL
/// Server directly; Infrastructure implements this port with plain ADO.NET,
/// mirroring the existing GoogleTaxonomy/AttributeCatalog SQL Server adapters.
///
/// Intentionally exposes no hard-delete operation (docs task: "Yunu.Commerce
/// V9 - Canonical Taxonomy Lifecycle + Usage Guards"): Canonical Taxonomy is
/// historical, structural catalog data, and its normal lifecycle retirement
/// path is <see cref="CanonicalTaxonomyNodeStatus.Archived"/> via
/// <see cref="UpdateAsync"/>, not physical row deletion.
///
/// Concurrency (docs task: "Yunu.Commerce - Canonical Taxonomy Concurrency
/// Guard"): mutations use a persisted, technical Revision token that is
/// never modeled as a <see cref="CanonicalTaxonomyNode"/> business
/// invariant. <see cref="GetWithRevisionAsync"/> exposes the Revision the
/// caller must echo back as an optimistic-concurrency guard to
/// <see cref="UpdateAsync"/> or <see cref="AddChildAsync"/>; a stale caller
/// (whose expected Revision no longer matches the persisted value) fails
/// explicitly instead of silently overwriting newer state (first-writer-wins).
/// </summary>
public interface ICanonicalTaxonomyRepository
{
    /// <summary>
    /// Persists a new root node (no ParentId). Returns the SQL Server
    /// IDENTITY-assigned id, since <see cref="CanonicalTaxonomyNode"/> is
    /// constructed by Application before the real id is known (docs task:
    /// "Canonical Taxonomy + Segments Domain" §19-§21). Root creation has no
    /// parent to coordinate with, so it carries no concurrency token.
    /// </summary>
    Task<CanonicalTaxonomyNodeId> AddAsync(CanonicalTaxonomyNode node, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a new child node (<paramref name="node"/>.ParentId must be
    /// set), atomically coordinated with the parent's own Revision (docs
    /// task: "Yunu.Commerce - Canonical Taxonomy Concurrency Guard" §7): the
    /// parent's current Status/Revision are re-checked and its Revision is
    /// incremented in the same transaction as the child insert, so a
    /// concurrent Archive of the parent and this call cannot both commit.
    /// </summary>
    Task<AddCanonicalTaxonomyChildResult> AddChildAsync(
        CanonicalTaxonomyNode node,
        long expectedParentRevision,
        CancellationToken cancellationToken);

    /// <summary>
    /// Conditionally persists changes made to an already-loaded node,
    /// using <paramref name="expectedRevision"/> as an optimistic-concurrency
    /// guard against the currently persisted Revision (docs task:
    /// "Yunu.Commerce - Canonical Taxonomy Concurrency Guard"). Returns
    /// <c>false</c> when no row matched (either the node does not exist, or
    /// its persisted Revision no longer equals <paramref name="expectedRevision"/>
    /// because another writer already committed a change); the caller must
    /// NOT reload and retry automatically, but surface an explicit
    /// concurrency conflict instead. On success, the persisted Revision is
    /// incremented by exactly 1.
    /// </summary>
    Task<bool> UpdateAsync(CanonicalTaxonomyNode node, long expectedRevision, CancellationToken cancellationToken);

    Task<CanonicalTaxonomyNode?> GetByIdAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a node together with its current persisted Revision (docs
    /// task: "Yunu.Commerce - Canonical Taxonomy Concurrency Guard"). Used
    /// by Catalog.Application immediately before a mutation, so the loaded
    /// Revision can be echoed back to <see cref="UpdateAsync"/> or
    /// <see cref="AddChildAsync"/> as the expected value. Revision is
    /// intentionally not a property of <see cref="CanonicalTaxonomyNode"/>
    /// itself: it is a technical persistence token, not a business
    /// invariant of the Aggregate.
    /// </summary>
    Task<(CanonicalTaxonomyNode Node, long Revision)?> GetWithRevisionAsync(
        CanonicalTaxonomyNodeId id,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetChildrenAsync(CanonicalTaxonomyNodeId parentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the root nodes of the tree (ParentId = null), ordered by Path
    /// (docs task: "CQRS de leitura e endpoints GET para Segments e
    /// Canonical Taxonomy" §3).
    /// </summary>
    Task<IReadOnlyCollection<CanonicalTaxonomyNode>> GetRootsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether the given node currently has any children.
    /// Catalog.Application to derive leaf-ness before allowing Update/Delete
    /// (docs task §22).
    /// </summary>
    Task<bool> HasChildrenAsync(CanonicalTaxonomyNodeId id, CancellationToken cancellationToken);
}
