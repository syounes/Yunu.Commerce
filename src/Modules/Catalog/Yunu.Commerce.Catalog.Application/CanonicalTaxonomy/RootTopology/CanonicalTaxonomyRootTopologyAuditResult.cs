namespace Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

/// <summary>
/// Result of <see cref="ICanonicalTaxonomyRootTopologyAuditor.Audit"/>.
/// </summary>
public sealed class CanonicalTaxonomyRootTopologyAuditResult
{
    public required CanonicalTaxonomyRootTopologyAuditOutcome Outcome { get; init; }

    public required string Message { get; init; }

    public static CanonicalTaxonomyRootTopologyAuditResult Valid(string message) => new()
    {
        Outcome = CanonicalTaxonomyRootTopologyAuditOutcome.Valid,
        Message = message
    };

    public static CanonicalTaxonomyRootTopologyAuditResult Invalid(
        CanonicalTaxonomyRootTopologyAuditOutcome outcome, string message) => new()
    {
        Outcome = outcome,
        Message = message
    };
}
