using System.Text.Json;
using Xunit;
using Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure.Tests.IntentRewriting;

/// <summary>
/// Verifies deserialization of the model's Structured Output JSON into
/// <see cref="IntentRewriteModelResponse"/>, without calling Azure OpenAI.
/// </summary>
public sealed class IntentRewriteModelResponseTests
{
    [Fact]
    public void Deserializes_product_creation_payload_with_attribute_hints()
    {
        const string json = """
            {
              "normalizedQuery": "Cadastrar um tênis masculino preto da Nike, tamanho 41, indicado para corrida.",
              "semanticQuery": "Tênis masculino preto para corrida, marca Nike, tamanho 41.",
              "intent": "ProductCreation",
              "detectedLanguage": "pt",
              "categoryHint": "Tênis para corrida",
              "attributeHints": [
                { "name": "gênero", "value": "masculino" },
                { "name": "marca", "value": "Nike" }
              ],
              "searchTerms": ["tênis", "masculino", "Nike"],
              "confidence": 0.96
            }
            """;

        var result = JsonSerializer.Deserialize<IntentRewriteModelResponse>(json)!;

        Assert.Equal("ProductCreation", result.Intent);
        Assert.Equal(2, result.AttributeHints.Count);
        Assert.Equal("Nike", result.AttributeHints[1].Value);
        Assert.Equal(0.96m, result.Confidence);
    }

    [Fact]
    public void Deserializes_unknown_intent_with_empty_arrays()
    {
        const string json = """
            {
              "normalizedQuery": "asdkjaslkdj",
              "semanticQuery": "asdkjaslkdj",
              "intent": "Unknown",
              "detectedLanguage": "und",
              "categoryHint": null,
              "attributeHints": [],
              "searchTerms": [],
              "confidence": 0.1
            }
            """;

        var result = JsonSerializer.Deserialize<IntentRewriteModelResponse>(json)!;

        Assert.Equal("Unknown", result.Intent);
        Assert.Empty(result.AttributeHints);
        Assert.Empty(result.SearchTerms);
        Assert.Null(result.CategoryHint);
    }
}
