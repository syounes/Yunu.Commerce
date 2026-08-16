using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure.Tests.IntentRewriting;

public sealed class IntentRewriteJsonSchemaTests
{
    [Fact]
    public void Build_requires_all_fields_and_disallows_additional_properties()
    {
        var schema = JsonNode.Parse(IntentRewriteJsonSchema.Build().ToString())!.AsObject();

        Assert.Equal("object", schema["type"]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());

        var required = schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();

        Assert.Equal(
            new[] { "normalizedQuery", "semanticQuery", "intent", "detectedLanguage", "categoryHint", "categorySearchQuery", "attributeHints", "searchTerms", "confidence" },
            required);
    }

    [Fact]
    public void Build_declares_categorySearchQuery_as_nullable_string()
    {
        var schema = JsonNode.Parse(IntentRewriteJsonSchema.Build().ToString())!.AsObject();

        var categorySearchQueryTypes = schema["properties"]!["categorySearchQuery"]!["type"]!.AsArray()
            .Select(n => n!.GetValue<string>())
            .ToArray();

        Assert.Equal(new[] { "string", "null" }, categorySearchQueryTypes);
    }

    [Fact]
    public void Build_restricts_intent_to_enum_values()
    {
        var schema = JsonNode.Parse(IntentRewriteJsonSchema.Build().ToString())!.AsObject();

        var intentEnum = schema["properties"]!["intent"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();

        Assert.Equal(new[] { "CatalogSearch", "ProductCreation", "ProductUpdate", "Unknown" }, intentEnum);
    }

    [Fact]
    public void Build_constrains_confidence_between_zero_and_one()
    {
        var schema = JsonNode.Parse(IntentRewriteJsonSchema.Build().ToString())!.AsObject();

        var confidence = schema["properties"]!["confidence"]!.AsObject();

        Assert.Equal(0, confidence["minimum"]!.GetValue<int>());
        Assert.Equal(1, confidence["maximum"]!.GetValue<int>());
    }

    [Fact]
    public void Build_declares_attributeHints_and_searchTerms_as_arrays()
    {
        var schema = JsonNode.Parse(IntentRewriteJsonSchema.Build().ToString())!.AsObject();

        Assert.Equal("array", schema["properties"]!["attributeHints"]!["type"]!.GetValue<string>());
        Assert.Equal("array", schema["properties"]!["searchTerms"]!["type"]!.GetValue<string>());
    }
}
