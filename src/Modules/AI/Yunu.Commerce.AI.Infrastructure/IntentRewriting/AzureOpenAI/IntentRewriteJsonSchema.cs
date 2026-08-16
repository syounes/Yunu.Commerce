using System.Text.Json.Nodes;

namespace Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

/// <summary>
/// Builds the strict JSON Schema enforced via Azure OpenAI Structured Outputs
/// for the Intent/Query Rewriter (docs task: "Intent/Query Rewriting"). All
/// properties are required, additional properties are disallowed, and
/// <c>intent</c>/<c>confidence</c> are constrained so the response is always
/// deterministic and directly deserializable into <see
/// cref="IntentRewriteModelResponse"/> without post-hoc validation.
/// </summary>
internal static class IntentRewriteJsonSchema
{
    public const string SchemaName = "intent_rewrite_result";

    public static BinaryData Build()
    {
        var attributeHintSchema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["rawName"] = new JsonObject { ["type"] = "string" },
                ["rawValue"] = new JsonObject { ["type"] = new JsonArray("string", "null") }
            },
            ["required"] = new JsonArray("rawName", "rawValue")
        };

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["normalizedQuery"] = new JsonObject { ["type"] = "string" },
                ["semanticQuery"] = new JsonObject { ["type"] = "string" },
                ["intent"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("CatalogSearch", "ProductCreation", "ProductUpdate", "Unknown")
                },
                ["detectedLanguage"] = new JsonObject { ["type"] = "string" },
                ["categoryHint"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
                ["categorySearchQuery"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
                ["attributeHints"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = attributeHintSchema
                },
                ["searchTerms"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" }
                },
                ["confidence"] = new JsonObject
                {
                    ["type"] = "number",
                    ["minimum"] = 0,
                    ["maximum"] = 1
                }
            },
            ["required"] = new JsonArray(
                "normalizedQuery",
                "semanticQuery",
                "intent",
                "detectedLanguage",
                "categoryHint",
                "categorySearchQuery",
                "attributeHints",
                "searchTerms",
                "confidence")
        };

        return BinaryData.FromString(schema.ToJsonString());
    }
}
