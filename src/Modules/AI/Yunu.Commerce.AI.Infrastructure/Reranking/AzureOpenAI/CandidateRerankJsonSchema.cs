using System.Text.Json.Nodes;

namespace Yunu.Commerce.AI.Infrastructure.Reranking.AzureOpenAI;

/// <summary>
/// Builds the strict JSON Schema enforced via Azure OpenAI Structured Outputs
/// for the Candidate Reranker (docs task: "Contextual candidate reranking").
/// All fields are required, additional properties are disallowed, and
/// <c>decision</c>/<c>confidence</c>/<c>relevanceScore</c> are constrained so
/// the response is always deterministic and directly deserializable into
/// <see cref="CandidateRerankModelResponse"/>. Deliberately does not include
/// any field for an official ID/code: the model can only ever return a
/// candidate index (docs restriction: "não retornar IDs").
/// </summary>
internal static class CandidateRerankJsonSchema
{
    public const string SchemaName = "candidate_rerank_result";

    public static BinaryData Build()
    {
        var rankingEntrySchema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["candidateIndex"] = new JsonObject { ["type"] = "integer" },
                ["relevanceScore"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                }
            },
            ["required"] = new JsonArray("candidateIndex", "relevanceScore")
        };

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["decision"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("Selected", "Ambiguous", "None")
                },
                ["selectedCandidateIndex"] = new JsonObject { ["type"] = new JsonArray("integer", "null") },
                ["confidence"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                },
                ["ranking"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = rankingEntrySchema
                },
                ["reason"] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray(
                "decision",
                "selectedCandidateIndex",
                "confidence",
                "ranking",
                "reason")
        };

        return BinaryData.FromString(schema.ToJsonString());
    }
}
