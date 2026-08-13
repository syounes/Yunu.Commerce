using Xunit;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;

namespace Yunu.Commerce.Catalog.Application.Tests.GoogleTaxonomy;

public class GoogleTaxonomyParserTests
{
    [Fact]
    public void ParseLine_With_RootCategory_Should_Return_Level_Zero()
    {
        var row = GoogleTaxonomyParser.ParseLine("166 - Apparel & Accessories");

        Assert.NotNull(row);
        Assert.Equal(166, row!.GoogleCategoryId);
        Assert.Equal("Apparel & Accessories", row.Name);
        Assert.Equal("Apparel & Accessories", row.FullPath);
        Assert.Equal(0, row.Level);
    }

    [Fact]
    public void ParseLine_With_NestedCategory_Should_Compute_Level_And_LastSegment_As_Name()
    {
        var row = GoogleTaxonomyParser.ParseLine("2271 - Apparel & Accessories > Clothing > Dresses");

        Assert.NotNull(row);
        Assert.Equal(2271, row!.GoogleCategoryId);
        Assert.Equal("Dresses", row.Name);
        Assert.Equal("Apparel & Accessories > Clothing > Dresses", row.FullPath);
        Assert.Equal(2, row.Level);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseLine_With_BlankLine_Should_Return_Null(string line)
    {
        Assert.Null(GoogleTaxonomyParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_With_CommentLine_Should_Return_Null()
    {
        Assert.Null(GoogleTaxonomyParser.ParseLine("# Google_Product_Taxonomy_Version: 2021-09-21"));
    }

    [Theory]
    [InlineData("not a valid line")]
    [InlineData("abc - Apparel & Accessories")]
    [InlineData("166 -")]
    [InlineData("166 -    ")]
    public void ParseLine_With_MalformedLine_Should_Return_Null(string line)
    {
        Assert.Null(GoogleTaxonomyParser.ParseLine(line));
    }

    [Fact]
    public void Parse_With_MixedValidAndInvalidLines_Should_Skip_Invalid_Lines()
    {
        var lines = new[]
        {
            "# comment header",
            "",
            "166 - Apparel & Accessories",
            "malformed line",
            "187 - Apparel & Accessories > Shoes"
        };

        var rows = GoogleTaxonomyParser.Parse(lines);

        Assert.Equal(2, rows.Count);
    }
}
