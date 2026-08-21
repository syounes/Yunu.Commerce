using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy.RootTopology;

public sealed class CanonicalTaxonomyRootTopologyAuditorTests
{
    private static CanonicalTaxonomyNode Root(string code, string name = "Root") =>
        CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0), code, name, name.ToLowerInvariant(), null, name,
            status: CanonicalTaxonomyNodeStatus.Active);

    private static CanonicalTaxonomyRootTopologyAuditor CreateSingleRootAuditor(
        string primaryRootCode = "catalog", string primaryRootName = "Catalog") =>
        new(Options.Create(new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.SingleRoot,
            PrimaryRootCode = primaryRootCode,
            PrimaryRootName = primaryRootName
        }));

    private static CanonicalTaxonomyRootTopologyAuditor CreateMultipleRootsAuditor() =>
        new(Options.Create(new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.MultipleRoots,
            PrimaryRootCode = null,
            PrimaryRootName = null
        }));

    [Fact]
    public void SingleRoot_Audit_With_Exactly_One_Matching_Root_Is_Valid()
    {
        var auditor = CreateSingleRootAuditor();

        var result = auditor.Audit(new[] { Root("catalog") });

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void SingleRoot_Audit_With_Zero_Roots_Is_Invalid()
    {
        var auditor = CreateSingleRootAuditor();

        var result = auditor.Audit(Array.Empty<CanonicalTaxonomyNode>());

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.NoRootFound, result.Outcome);
    }

    [Fact]
    public void SingleRoot_Audit_With_Multiple_Roots_Is_Invalid()
    {
        var auditor = CreateSingleRootAuditor();

        var result = auditor.Audit(new[] { Root("catalog"), Root("other") });

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.MultipleRootsFoundForSingleRootPolicy, result.Outcome);
    }

    [Fact]
    public void SingleRoot_Audit_Where_Configured_Root_Code_Is_Absent_Is_Invalid()
    {
        var auditor = CreateSingleRootAuditor(primaryRootCode: "catalog");

        var result = auditor.Audit(new[] { Root("not-catalog") });

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.ConfiguredPrimaryRootNotFound, result.Outcome);
    }

    [Fact]
    public void MultipleRoots_Audit_Accepts_Multiple_Root_Nodes()
    {
        var auditor = CreateMultipleRootsAuditor();

        var result = auditor.Audit(new[] { Root("electronics"), Root("fashion"), Root("home") });

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void MultipleRoots_Audit_Accepts_A_Single_Root_Node()
    {
        var auditor = CreateMultipleRootsAuditor();

        var result = auditor.Audit(new[] { Root("catalog") });

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void MultipleRoots_Audit_Accepts_Zero_Roots()
    {
        var auditor = CreateMultipleRootsAuditor();

        var result = auditor.Audit(Array.Empty<CanonicalTaxonomyNode>());

        Assert.Equal(CanonicalTaxonomyRootTopologyAuditOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void Audit_Rejects_A_Node_With_ParentId_Set()
    {
        var auditor = CreateSingleRootAuditor();

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(2), new CanonicalTaxonomyNodeId(1), "child", "Child", "child", null, 1, "Root > Child",
            status: CanonicalTaxonomyNodeStatus.Active);

        Assert.Throws<ArgumentException>(() => auditor.Audit(new[] { child }));
    }
}
