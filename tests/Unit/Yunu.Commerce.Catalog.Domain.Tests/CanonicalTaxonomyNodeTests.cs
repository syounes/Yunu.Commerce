using Xunit;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Domain.Tests;

public class CanonicalTaxonomyNodeTests
{
    private static CanonicalTaxonomyNode CreateDraftRoot(string code = "root") =>
        CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            code,
            "Root",
            "root",
            null,
            "Root");

    [Fact]
    public void CreateRoot_defaults_to_draft_status()
    {
        var node = CreateDraftRoot();

        Assert.Equal(CanonicalTaxonomyNodeStatus.Draft, node.Status);
    }

    [Theory]
    [InlineData(CanonicalTaxonomyNodeStatus.Draft, CanonicalTaxonomyNodeStatus.Active)]
    [InlineData(CanonicalTaxonomyNodeStatus.Draft, CanonicalTaxonomyNodeStatus.Archived)]
    [InlineData(CanonicalTaxonomyNodeStatus.Active, CanonicalTaxonomyNodeStatus.Inactive)]
    [InlineData(CanonicalTaxonomyNodeStatus.Active, CanonicalTaxonomyNodeStatus.Archived)]
    [InlineData(CanonicalTaxonomyNodeStatus.Inactive, CanonicalTaxonomyNodeStatus.Active)]
    [InlineData(CanonicalTaxonomyNodeStatus.Inactive, CanonicalTaxonomyNodeStatus.Archived)]
    public void TransitionTo_allows_documented_transitions(
        CanonicalTaxonomyNodeStatus from,
        CanonicalTaxonomyNodeStatus to)
    {
        var node = CreateDraftRoot();

        // Walk to the starting status first, since Draft is the only initial status.
        if (from != CanonicalTaxonomyNodeStatus.Draft)
        {
            node.TransitionTo(CanonicalTaxonomyNodeStatus.Active);
            if (from == CanonicalTaxonomyNodeStatus.Inactive)
            {
                node.TransitionTo(CanonicalTaxonomyNodeStatus.Inactive);
            }
        }

        node.TransitionTo(to);

        Assert.Equal(to, node.Status);
    }

    [Theory]
    [InlineData(CanonicalTaxonomyNodeStatus.Archived, CanonicalTaxonomyNodeStatus.Draft)]
    [InlineData(CanonicalTaxonomyNodeStatus.Archived, CanonicalTaxonomyNodeStatus.Active)]
    [InlineData(CanonicalTaxonomyNodeStatus.Archived, CanonicalTaxonomyNodeStatus.Inactive)]
    public void TransitionTo_from_archived_always_throws(
        CanonicalTaxonomyNodeStatus from,
        CanonicalTaxonomyNodeStatus to)
    {
        var node = CreateDraftRoot();
        node.TransitionTo(CanonicalTaxonomyNodeStatus.Archived);

        Assert.Equal(CanonicalTaxonomyNodeStatus.Archived, from);

        Assert.Throws<InvalidCanonicalTaxonomyNodeStatusTransitionException>(
            () => node.TransitionTo(to));
    }

    [Fact]
    public void TransitionTo_same_status_is_a_no_op()
    {
        var node = CreateDraftRoot();
        node.TransitionTo(CanonicalTaxonomyNodeStatus.Active);

        node.TransitionTo(CanonicalTaxonomyNodeStatus.Active);

        Assert.Equal(CanonicalTaxonomyNodeStatus.Active, node.Status);
    }

    [Fact]
    public void TransitionTo_draft_to_inactive_directly_throws()
    {
        var node = CreateDraftRoot();

        Assert.Throws<InvalidCanonicalTaxonomyNodeStatusTransitionException>(
            () => node.TransitionTo(CanonicalTaxonomyNodeStatus.Inactive));
    }

    [Fact]
    public void Update_on_archived_node_throws_and_does_not_mutate_state()
    {
        var node = CreateDraftRoot();
        node.TransitionTo(CanonicalTaxonomyNodeStatus.Archived);
        var eventCountBeforeUpdate = node.DomainEvents.Count;

        Assert.Throws<CanonicalTaxonomyNodeArchivedException>(
            () => node.Update("New Name", "new name", "New Description", "New Path"));

        Assert.Equal("Root", node.Name);
        Assert.Equal("root", node.NormalizedName);
        Assert.Null(node.Description);
        Assert.Equal("Root", node.Path);
        Assert.Equal(eventCountBeforeUpdate, node.DomainEvents.Count);
    }
}
