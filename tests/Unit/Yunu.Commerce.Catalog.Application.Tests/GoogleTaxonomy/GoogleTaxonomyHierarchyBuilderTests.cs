using Xunit;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.GoogleTaxonomy;

public class GoogleTaxonomyHierarchyBuilderTests
{
    [Fact]
    public void Build_With_EmptyFeed_Should_Throw()
    {
        Assert.Throws<GoogleTaxonomyValidationException>(
            () => GoogleTaxonomyHierarchyBuilder.Build(Array.Empty<ParsedGoogleTaxonomyRow>()));
    }

    [Fact]
    public void Build_Should_Resolve_ParentChild_Relationships_And_Levels()
    {
        var rows = new[]
        {
            new ParsedGoogleTaxonomyRow(166, "Apparel & Accessories", "Apparel & Accessories", 0),
            new ParsedGoogleTaxonomyRow(187, "Shoes", "Apparel & Accessories > Shoes", 1),
            new ParsedGoogleTaxonomyRow(188, "Athletic Shoes", "Apparel & Accessories > Shoes > Athletic Shoes", 2)
        };

        var categories = GoogleTaxonomyHierarchyBuilder.Build(rows).ToDictionary(c => c.GoogleCategoryId);

        Assert.Null(categories[166].ParentGoogleCategoryId);
        Assert.Equal(166, categories[187].ParentGoogleCategoryId);
        Assert.Equal(187, categories[188].ParentGoogleCategoryId);

        Assert.Equal(0, categories[166].Level);
        Assert.Equal(1, categories[187].Level);
        Assert.Equal(2, categories[188].Level);
    }

    [Fact]
    public void Build_Should_Mark_OnlyLeafCategories_As_IsLeaf()
    {
        var rows = new[]
        {
            new ParsedGoogleTaxonomyRow(166, "Apparel & Accessories", "Apparel & Accessories", 0),
            new ParsedGoogleTaxonomyRow(187, "Shoes", "Apparel & Accessories > Shoes", 1),
            new ParsedGoogleTaxonomyRow(188, "Athletic Shoes", "Apparel & Accessories > Shoes > Athletic Shoes", 2)
        };

        var categories = GoogleTaxonomyHierarchyBuilder.Build(rows).ToDictionary(c => c.GoogleCategoryId);

        Assert.False(categories[166].IsLeaf);
        Assert.False(categories[187].IsLeaf);
        Assert.True(categories[188].IsLeaf);
    }

    [Fact]
    public void Build_With_DuplicateIds_Should_Throw()
    {
        var rows = new[]
        {
            new ParsedGoogleTaxonomyRow(166, "Apparel & Accessories", "Apparel & Accessories", 0),
            new ParsedGoogleTaxonomyRow(166, "Apparel & Accessories Duplicate", "Apparel & Accessories Duplicate", 0)
        };

        Assert.Throws<GoogleTaxonomyValidationException>(() => GoogleTaxonomyHierarchyBuilder.Build(rows));
    }

    [Fact]
    public void Build_With_DuplicateFullPaths_Should_Throw()
    {
        var rows = new[]
        {
            new ParsedGoogleTaxonomyRow(166, "Apparel & Accessories", "Apparel & Accessories", 0),
            new ParsedGoogleTaxonomyRow(167, "Apparel & Accessories", "Apparel & Accessories", 0)
        };

        Assert.Throws<GoogleTaxonomyValidationException>(() => GoogleTaxonomyHierarchyBuilder.Build(rows));
    }

    [Fact]
    public void Build_With_MissingParentPath_Should_Throw()
    {
        var rows = new[]
        {
            new ParsedGoogleTaxonomyRow(188, "Athletic Shoes", "Apparel & Accessories > Shoes > Athletic Shoes", 2)
        };

        Assert.Throws<GoogleTaxonomyValidationException>(() => GoogleTaxonomyHierarchyBuilder.Build(rows));
    }
}
