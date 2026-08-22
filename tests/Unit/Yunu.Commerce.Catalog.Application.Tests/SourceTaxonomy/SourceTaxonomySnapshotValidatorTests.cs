using Xunit;
using Yunu.Commerce.Catalog.Application.SourceTaxonomy.Import;

namespace Yunu.Commerce.Catalog.Application.Tests.SourceTaxonomy;

public class SourceTaxonomySnapshotValidatorTests
{
    private static SourceTaxonomySnapshotDescriptor Descriptor(string providerCode = "google", string locale = "pt-BR") => new()
    {
        ProviderCode = providerCode,
        Locale = locale,
        ScopeCode = null,
        ExternalTaxonomyId = null,
        ExternalVersion = "v1",
        SourceUri = "https://example.com",
        SourceChecksum = "abc"
    };

    private static SourceTaxonomySnapshotNode Node(
        string externalNodeId,
        string? parentExternalNodeId = null,
        string nodeType = "Category",
        int level = 0) => new()
    {
        ExternalNodeId = externalNodeId,
        ParentExternalNodeId = parentExternalNodeId,
        NodeType = nodeType,
        Name = $"Name {externalNodeId}",
        FullPath = $"Root > {externalNodeId}",
        Level = level,
        IsLeaf = true,
        IsActive = true
    };

    [Fact]
    public void Validate_With_ValidSnapshot_Should_Not_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1"), Node("2", "1", level: 1) }
        };

        var exception = Record.Exception(() => SourceTaxonomySnapshotValidator.Validate(snapshot));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_With_NullSnapshot_Should_Throw()
    {
        Assert.Throws<SourceTaxonomySnapshotValidationException>(() => SourceTaxonomySnapshotValidator.Validate(null));
    }

    [Fact]
    public void Validate_With_EmptyNodes_Should_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = Array.Empty<SourceTaxonomySnapshotNode>()
        };

        Assert.Throws<SourceTaxonomySnapshotValidationException>(() => SourceTaxonomySnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Validate_With_DuplicateExternalNodeId_Should_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1"), Node("1") }
        };

        Assert.Throws<SourceTaxonomySnapshotValidationException>(() => SourceTaxonomySnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Validate_With_MissingParentReference_Should_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1", "missing-parent") }
        };

        Assert.Throws<SourceTaxonomySnapshotValidationException>(() => SourceTaxonomySnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Validate_With_SelfParent_Should_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1", "1") }
        };

        Assert.Throws<SourceTaxonomySnapshotValidationException>(() => SourceTaxonomySnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Validate_With_ParentCycle_Should_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1", "2"), Node("2", "1") }
        };

        Assert.Throws<SourceTaxonomySnapshotValidationException>(() => SourceTaxonomySnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Validate_With_MultipleRoots_Should_Not_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1"), Node("2") }
        };

        var exception = Record.Exception(() => SourceTaxonomySnapshotValidator.Validate(snapshot));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_With_ArbitraryNodeType_Should_Not_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("1", nodeType: "BrowseNode") }
        };

        var exception = Record.Exception(() => SourceTaxonomySnapshotValidator.Validate(snapshot));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_With_NonNumericExternalNodeId_Should_Not_Throw()
    {
        var snapshot = new SourceTaxonomySnapshot
        {
            Descriptor = Descriptor(),
            Nodes = new[] { Node("MLB1055") }
        };

        var exception = Record.Exception(() => SourceTaxonomySnapshotValidator.Validate(snapshot));

        Assert.Null(exception);
    }
}
