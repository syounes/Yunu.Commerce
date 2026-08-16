using System.Text.Json.Nodes;
using Xunit;
using Yunu.Commerce.AI.Infrastructure.Reranking.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure.Tests.Reranking;

public sealed class CandidateRerankJsonSchemaTests
{
    [Fact]
    public void Build_requires_all_fields_and_disallows_additional_properties()
    {
        var schema = JsonNode.Parse(CandidateRerankJsonSchema.Build().ToString())!.AsObject();

        Assert.Equal("object", schema["type"]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());

        var required = schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();

        Assert.Equal(
            new[] { "decision", "selectedCandidateIndex", "confidence", "ranking", "reason" },
            required);
    }

    [Fact]
    public void Build_restricts_decision_to_enum_values()
    {
        var schema = JsonNode.Parse(CandidateRerankJsonSchema.Build().ToString())!.AsObject();

        var decisionEnum = schema["properties"]!["decision"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();

        Assert.Equal(new[] { "Selected", "Ambiguous", "None" }, decisionEnum);
    }

    [Fact]
    public void Build_constrains_confidence_between_zero_and_one()
    {
        var schema = JsonNode.Parse(CandidateRerankJsonSchema.Build().ToString())!.AsObject();

        var confidence = schema["properties"]!["confidence"]!.AsObject();

        Assert.Equal(0, confidence["minimum"]!.GetValue<int>());
        Assert.Equal(1, confidence["maximum"]!.GetValue<int>());
    }

    [Fact]
    public void Build_declares_ranking_as_array_with_no_additional_properties_per_item()
    {
        var schema = JsonNode.Parse(CandidateRerankJsonSchema.Build().ToString())!.AsObject();

        var ranking = schema["properties"]!["ranking"]!.AsObject();
        Assert.Equal("array", ranking["type"]!.GetValue<string>());

        var item = ranking["items"]!.AsObject();
        Assert.False(item["additionalProperties"]!.GetValue<bool>());

        var required = item["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "candidateIndex", "relevanceScore" }, required);
    }

    [Fact]
    public void Build_allows_selectedCandidateIndex_to_be_null()
    {
        var schema = JsonNode.Parse(CandidateRerankJsonSchema.Build().ToString())!.AsObject();

        var type = schema["properties"]!["selectedCandidateIndex"]!["type"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();

        Assert.Equal(new[] { "integer", "null" }, type);
    }
}
