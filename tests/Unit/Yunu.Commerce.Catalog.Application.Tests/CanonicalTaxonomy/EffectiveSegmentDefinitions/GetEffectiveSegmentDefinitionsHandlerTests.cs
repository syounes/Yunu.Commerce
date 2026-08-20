using Xunit;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy.EffectiveSegmentDefinitions;

public class GetEffectiveSegmentDefinitionsHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_Empty_When_Node_Does_Not_Exist()
    {
        var canonicalTaxonomyRepository = new FakeCanonicalTaxonomyRepository();
        var associationReader = new FakeCanonicalTaxonomySegmentAssociationReader();
        var handler = new GetEffectiveSegmentDefinitionsHandler(canonicalTaxonomyRepository, associationReader);

        var result = await handler.HandleAsync(
            new GetEffectiveSegmentDefinitionsQuery { CanonicalTaxonomyNodeId = 999_999 },
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_When_Node_Exists_But_Has_No_Segments()
    {
        var canonicalTaxonomyRepository = new FakeCanonicalTaxonomyRepository();
        var node = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(1), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var nodeId = await canonicalTaxonomyRepository.AddAsync(node, CancellationToken.None);

        var associationReader = new FakeCanonicalTaxonomySegmentAssociationReader();
        var handler = new GetEffectiveSegmentDefinitionsHandler(canonicalTaxonomyRepository, associationReader);

        var result = await handler.HandleAsync(
            new GetEffectiveSegmentDefinitionsQuery { CanonicalTaxonomyNodeId = nodeId.Value },
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_Should_Delegate_To_Resolver_And_Return_Effective_Segments()
    {
        var canonicalTaxonomyRepository = new FakeCanonicalTaxonomyRepository();
        var node = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(1), "cat", "Catálogo", "catalogo", null, "Catálogo",
            status: CanonicalTaxonomyNodeStatus.Active);
        var nodeId = await canonicalTaxonomyRepository.AddAsync(node, CancellationToken.None);

        var associationReader = new FakeCanonicalTaxonomySegmentAssociationReader();
        associationReader.Setup(nodeId.Value, new[]
        {
            new CanonicalTaxonomySegmentAssociationCandidate
            {
                OriginCanonicalTaxonomyNodeId = nodeId.Value,
                OriginNodeDepth = 0,
                IsSelf = true,
                AppliesToDescendants = false,
                AssociationStatus = "Approved",
                AssociationSource = "Yunu",
                AssociationIsRequired = true,
                SegmentDefinitionId = 100,
                Code = "gender",
                Name = "Gender",
                DefinitionStatus = "Active",
                AssignmentScope = "Product"
            }
        });

        var handler = new GetEffectiveSegmentDefinitionsHandler(canonicalTaxonomyRepository, associationReader);

        var result = await handler.HandleAsync(
            new GetEffectiveSegmentDefinitionsQuery { CanonicalTaxonomyNodeId = nodeId.Value },
            CancellationToken.None);

        var single = Assert.Single(result);
        Assert.Equal("gender", single.Code);
        Assert.True(single.IsDirect);
        Assert.True(single.IsRequired);
    }
}
