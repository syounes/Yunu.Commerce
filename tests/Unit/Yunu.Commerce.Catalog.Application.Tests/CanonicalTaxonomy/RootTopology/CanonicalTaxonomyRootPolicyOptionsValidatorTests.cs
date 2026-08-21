using Microsoft.Extensions.Options;
using Xunit;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.RootTopology;

namespace Yunu.Commerce.Catalog.Application.Tests.CanonicalTaxonomy.RootTopology;

public sealed class CanonicalTaxonomyRootPolicyOptionsValidatorTests
{
    private readonly CanonicalTaxonomyRootPolicyOptionsValidator _validator = new();

    [Fact]
    public void SingleRoot_With_Valid_Code_And_Name_Is_Accepted()
    {
        var options = new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.SingleRoot,
            PrimaryRootCode = "catalog",
            PrimaryRootName = "Catalog"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void SingleRoot_Without_PrimaryRootCode_Is_Rejected()
    {
        var options = new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.SingleRoot,
            PrimaryRootCode = null,
            PrimaryRootName = "Catalog"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("PrimaryRootCode", result.FailureMessage);
    }

    [Fact]
    public void SingleRoot_Without_PrimaryRootCode_Whitespace_Is_Rejected()
    {
        var options = new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.SingleRoot,
            PrimaryRootCode = "   ",
            PrimaryRootName = "Catalog"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void SingleRoot_Without_PrimaryRootName_Is_Rejected()
    {
        var options = new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.SingleRoot,
            PrimaryRootCode = "catalog",
            PrimaryRootName = null
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("PrimaryRootName", result.FailureMessage);
    }

    [Fact]
    public void MultipleRoots_Does_Not_Require_A_Primary_Root()
    {
        var options = new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = CanonicalTaxonomyRootMode.MultipleRoots,
            PrimaryRootCode = null,
            PrimaryRootName = null
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Unsupported_RootMode_Is_Rejected()
    {
        var options = new CanonicalTaxonomyRootPolicyOptions
        {
            RootMode = (CanonicalTaxonomyRootMode)999,
            PrimaryRootCode = "catalog",
            PrimaryRootName = "Catalog"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }
}
