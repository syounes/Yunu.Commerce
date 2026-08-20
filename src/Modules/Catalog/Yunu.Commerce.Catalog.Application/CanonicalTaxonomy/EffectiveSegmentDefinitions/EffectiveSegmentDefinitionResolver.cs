namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

/// <summary>
/// Deterministically resolves the Effective Segment Definitions of a
/// Canonical Taxonomy node from raw association candidates (docs task:
/// "Effective Segment Definitions por Canonical Taxonomy Node"). No LLM,
/// embedding, pgvector or heuristic ranking participates in this resolution.
///
/// Precedence rule: for a given SegmentDefinitionId, the candidate whose
/// origin node is deepest (closest to / equal to the queried node) wins.
/// A direct association on the queried node itself is therefore always the
/// most specific and always wins over any inherited candidate for the same
/// Definition. An inherited candidate from a node is only considered at all
/// when AppliesToDescendants = true and the origin node is a strict ancestor
/// of the queried node (IsSelf = false); when the origin node is the queried
/// node itself, AppliesToDescendants is irrelevant (docs task rule 1).
///
/// Only candidates with AssociationStatus = "Approved" and
/// DefinitionStatus = "Active" are eligible; every other status
/// (Suggested/Rejected/Inactive for associations, Draft/Inactive/Archived
/// for definitions) is excluded before precedence is computed.
///
/// IsRequired semantics (docs task: "Consolidar a semântica de IsRequired em
/// Segments"): obligatoriness of a Segment Definition is exclusively
/// contextual to where it is associated in the Canonical Taxonomy.
/// Catalog.SegmentDefinitions no longer persists an IsRequired column at
/// all; Catalog.CanonicalTaxonomyNodeSegmentDefinitions.IsRequired is the
/// single source of truth. This resolver surfaces the winning association's
/// IsRequired value, because the same Definition can be optional in one
/// canonical branch and required in another (e.g. gender optional under
/// "Vestuário e acessórios" but potentially required under a more specific
/// descendant). Precedence is applied uniformly: the winning association
/// (by specificity) determines IsRequired together with every other
/// association-level field.
/// </summary>
public static class EffectiveSegmentDefinitionResolver
{
    private const string ApprovedAssociationStatus = "Approved";
    private const string ActiveDefinitionStatus = "Active";

    public static IReadOnlyCollection<EffectiveSegmentDefinitionResponse> Resolve(
        IEnumerable<CanonicalTaxonomySegmentAssociationCandidate> candidates)
    {
        var eligible = candidates.Where(IsEligible);

        var winners = eligible
            .GroupBy(c => c.SegmentDefinitionId)
            .Select(SelectWinner)
            .OrderBy(c => c.Code, StringComparer.Ordinal);

        return winners
            .Select(ToResponse)
            .ToArray();
    }

    private static bool IsEligible(CanonicalTaxonomySegmentAssociationCandidate candidate)
    {
        if (!string.Equals(candidate.AssociationStatus, ApprovedAssociationStatus, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(candidate.DefinitionStatus, ActiveDefinitionStatus, StringComparison.Ordinal))
        {
            return false;
        }

        // Rule 3/4: an inherited (ancestor) association only counts when it
        // explicitly applies to descendants. A direct association on the
        // queried node itself always counts (rule 1/9), regardless of
        // AppliesToDescendants (that flag only governs propagation to
        // descendants, not the association's own node).
        return candidate.IsSelf || candidate.AppliesToDescendants;
    }

    private static CanonicalTaxonomySegmentAssociationCandidate SelectWinner(
        IGrouping<long, CanonicalTaxonomySegmentAssociationCandidate> group)
    {
        // Rule 8/9: the most specific (deepest) origin node wins; the
        // queried node's own direct association is always at least as deep
        // as any ancestor, so it naturally wins ties.
        return group.OrderByDescending(c => c.OriginNodeDepth).First();
    }

    private static EffectiveSegmentDefinitionResponse ToResponse(CanonicalTaxonomySegmentAssociationCandidate winner)
    {
        return new EffectiveSegmentDefinitionResponse
        {
            SegmentDefinitionId = winner.SegmentDefinitionId,
            Code = winner.Code,
            Name = winner.Name,
            AssignmentScope = winner.AssignmentScope,
            IsRequired = winner.AssociationIsRequired,
            AssociationSource = winner.AssociationSource,
            IsDirect = winner.IsSelf,
            OriginCanonicalTaxonomyNodeId = winner.OriginCanonicalTaxonomyNodeId
        };
    }
}
