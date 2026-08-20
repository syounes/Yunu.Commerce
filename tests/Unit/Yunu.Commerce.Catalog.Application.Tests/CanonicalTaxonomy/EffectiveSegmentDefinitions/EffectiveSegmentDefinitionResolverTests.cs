using Xunit;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy.EffectiveSegmentDefinitions;

public class EffectiveSegmentDefinitionResolverTests
{
    private static CanonicalTaxonomySegmentAssociationCandidate Candidate(
        long originNodeId,
        int depth,
        bool isSelf,
        bool appliesToDescendants,
        long segmentDefinitionId,
        string code,
        string associationStatus = "Approved",
        string definitionStatus = "Active",
        bool isRequired = false,
        string source = "Yunu",
        string name = null!,
        string assignmentScope = "Product")
    {
        return new CanonicalTaxonomySegmentAssociationCandidate
        {
            OriginCanonicalTaxonomyNodeId = originNodeId,
            OriginNodeDepth = depth,
            IsSelf = isSelf,
            AppliesToDescendants = appliesToDescendants,
            AssociationStatus = associationStatus,
            AssociationSource = source,
            AssociationIsRequired = isRequired,
            SegmentDefinitionId = segmentDefinitionId,
            Code = code,
            Name = name ?? code,
            AssignmentScope = assignmentScope,
            DefinitionStatus = definitionStatus
        };
    }

    [Fact]
    public void Resolve_Should_Return_Direct_Association()
    {
        var candidates = new[]
        {
            Candidate(originNodeId: 10, depth: 2, isSelf: true, appliesToDescendants: false, segmentDefinitionId: 100, code: "gender")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        var single = Assert.Single(result);
        Assert.Equal(100, single.SegmentDefinitionId);
        Assert.Equal("gender", single.Code);
        Assert.True(single.IsDirect);
        Assert.Equal(10, single.OriginCanonicalTaxonomyNodeId);
    }

    [Fact]
    public void Resolve_Should_Return_Multiple_Segments_On_Same_Node()
    {
        var candidates = new[]
        {
            Candidate(10, 2, true, false, 100, "gender"),
            Candidate(10, 2, true, false, 101, "size"),
            Candidate(10, 2, true, false, 102, "color")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Code == "gender");
        Assert.Contains(result, r => r.Code == "size");
        Assert.Contains(result, r => r.Code == "color");
    }

    [Fact]
    public void Resolve_Should_Inherit_When_AppliesToDescendants_Is_True()
    {
        // Parent(depth 0) -> Child(depth 1, queried). Association defined on Parent.
        var candidates = new[]
        {
            Candidate(originNodeId: 1, depth: 0, isSelf: false, appliesToDescendants: true, segmentDefinitionId: 100, code: "gender")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        var single = Assert.Single(result);
        Assert.Equal("gender", single.Code);
        Assert.False(single.IsDirect);
        Assert.Equal(1, single.OriginCanonicalTaxonomyNodeId);
    }

    [Fact]
    public void Resolve_Should_Not_Inherit_When_AppliesToDescendants_Is_False()
    {
        var candidates = new[]
        {
            Candidate(originNodeId: 1, depth: 0, isSelf: false, appliesToDescendants: false, segmentDefinitionId: 100, code: "gender")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_Should_Combine_Multiple_Levels_Of_Inheritance_And_Direct()
    {
        // Root(X, AppliesToDescendants=true) -> Parent(Y, AppliesToDescendants=true) -> Child(Z, direct)
        var candidates = new[]
        {
            Candidate(originNodeId: 1, depth: 0, isSelf: false, appliesToDescendants: true, segmentDefinitionId: 100, code: "X"),
            Candidate(originNodeId: 2, depth: 1, isSelf: false, appliesToDescendants: true, segmentDefinitionId: 101, code: "Y"),
            Candidate(originNodeId: 3, depth: 2, isSelf: true, appliesToDescendants: false, segmentDefinitionId: 102, code: "Z")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Code == "X" && !r.IsDirect && r.OriginCanonicalTaxonomyNodeId == 1);
        Assert.Contains(result, r => r.Code == "Y" && !r.IsDirect && r.OriginCanonicalTaxonomyNodeId == 2);
        Assert.Contains(result, r => r.Code == "Z" && r.IsDirect && r.OriginCanonicalTaxonomyNodeId == 3);
    }

    [Fact]
    public void Resolve_Should_Override_Ancestor_Association_With_Direct_Association_For_Same_Definition()
    {
        // Root(X, AppliesToDescendants=true), Child has its own direct association for X.
        var candidates = new[]
        {
            Candidate(originNodeId: 1, depth: 0, isSelf: false, appliesToDescendants: true, segmentDefinitionId: 100, code: "X"),
            Candidate(originNodeId: 3, depth: 2, isSelf: true, appliesToDescendants: false, segmentDefinitionId: 100, code: "X")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        var single = Assert.Single(result);
        Assert.Equal(100, single.SegmentDefinitionId);
        Assert.True(single.IsDirect);
        Assert.Equal(3, single.OriginCanonicalTaxonomyNodeId);
    }

    [Fact]
    public void Resolve_Should_Override_With_Intermediate_Ancestor_Association()
    {
        // Root(X, AppliesToDescendants=true) -> Parent(X, AppliesToDescendants=true) -> Child (queried, no direct assoc)
        var candidates = new[]
        {
            Candidate(originNodeId: 1, depth: 0, isSelf: false, appliesToDescendants: true, segmentDefinitionId: 100, code: "X"),
            Candidate(originNodeId: 2, depth: 1, isSelf: false, appliesToDescendants: true, segmentDefinitionId: 100, code: "X")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        var single = Assert.Single(result);
        Assert.Equal(100, single.SegmentDefinitionId);
        Assert.False(single.IsDirect);
        Assert.Equal(2, single.OriginCanonicalTaxonomyNodeId);
    }

    [Fact]
    public void Resolve_Should_Exclude_Inactive_SegmentDefinition()
    {
        var candidates = new[]
        {
            Candidate(10, 2, true, false, 100, "gender", definitionStatus: "Inactive")
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("Suggested")]
    [InlineData("Rejected")]
    [InlineData("Inactive")]
    public void Resolve_Should_Exclude_Non_Approved_Association(string associationStatus)
    {
        var candidates = new[]
        {
            Candidate(10, 2, true, false, 100, "gender", associationStatus: associationStatus)
        };

        var result = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_Should_Return_Empty_Collection_When_Node_Has_No_Segments()
    {
        var result = EffectiveSegmentDefinitionResolver.Resolve(Array.Empty<CanonicalTaxonomySegmentAssociationCandidate>());

        Assert.Empty(result);
    }
}
