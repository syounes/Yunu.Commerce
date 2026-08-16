using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.ClientModel.Primitives;
using Xunit;
using Yunu.Commerce.AI.Application.Configuration;
using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.AI.Infrastructure.IntentRewriting.AzureOpenAI;

namespace Yunu.Commerce.AI.Infrastructure.Tests.IntentRewriting;

/// <summary>
/// Unit tests for <see cref="AzureOpenAIIntentRewriter"/> covering the
/// minimal fix for the HTTP 503 investigation: explicit handling of
/// <c>finish_reason = length</c> (output truncation), preservation of the
/// existing success/content-filter/invalid-JSON behaviors, and coverage for
/// a long, fact-rich structured response. No real Azure OpenAI call is made;
/// an in-process fake <see cref="HttpMessageHandler"/> simulates provider
/// responses via <see cref="HttpClientPipelineTransport"/>.
/// </summary>
public sealed class AzureOpenAIIntentRewriterTests
{
    private static AzureOpenAIIntentRewriter CreateRewriter(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(responseJson, statusCode);
        var httpClient = new HttpClient(handler);
        var transport = new HttpClientPipelineTransport(httpClient);

        var options = new AIOptions
        {
            Connections =
            {
                ["AzureOpenAI"] = new AIConnectionOptions
                {
                    Endpoint = "https://example.invalid/openai/v1/",
                    ApiKey = "fake-key"
                }
            },
            Models =
            {
                ["IntentRewriter"] = new AIModelOptions
                {
                    Connection = "AzureOpenAI",
                    DeploymentName = "fake-intent-rewriter",
                    ModelType = AIModelType.Chat
                }
            }
        };

        var catalog = new AIModelCatalog(Options.Create(options));

        return new AzureOpenAIIntentRewriter(
            catalog,
            NullLogger<AzureOpenAIIntentRewriter>.Instance,
            transport);
    }

    [Fact]
    public async Task RewriteAsync_when_finish_reason_is_length_throws_OutputTruncated()
    {
        var responseJson = BuildChatCompletionResponseWithRawContent(
            """{"normalizedQuery":"incomplete"}""",
            "length",
            2000,
            150);

        var rewriter = CreateRewriter(responseJson);

        var ex = await Assert.ThrowsAsync<IntentRewriteException>(
            () => rewriter.RewriteAsync(new IntentRewriteRequest("entrada complexa com muitos fatos explícitos")));

        Assert.Equal(IntentRewriteFailureReason.OutputTruncated, ex.Reason);
    }

    [Fact]
    public async Task RewriteAsync_preserves_existing_success_behavior()
    {
        const string modelJson = """
            {
              "normalizedQuery": "Cadastrar um tênis masculino preto da Nike, tamanho 41.",
              "semanticQuery": "Tênis masculino preto Nike tamanho 41.",
              "intent": "ProductCreation",
              "detectedLanguage": "pt",
              "categoryHint": "Tênis para corrida",
              "categorySearchQuery": "sapatos esportivos para corrida",
              "attributeHints": [
                { "rawName": "gênero", "rawValue": "masculino" }
              ],
              "searchTerms": ["tênis", "Nike"],
              "confidence": 0.9
            }
            """;

        var rewriter = CreateRewriter(BuildChatCompletionResponseWithRawContent(modelJson, "stop", 120, 80));

        var result = await rewriter.RewriteAsync(new IntentRewriteRequest("quero cadastrar um tenis"));

        Assert.Equal(CatalogIntent.ProductCreation, result.Intent);
        Assert.Equal(0.9m, result.Confidence);
        Assert.Single(result.AttributeHints);
    }

    [Fact]
    public async Task RewriteAsync_when_content_is_invalid_json_throws_InvalidResponse()
    {
        var responseJson = BuildChatCompletionResponseWithRawContent("{ this is not valid json", "stop", 50, 20);

        var rewriter = CreateRewriter(responseJson);

        var ex = await Assert.ThrowsAsync<IntentRewriteException>(
            () => rewriter.RewriteAsync(new IntentRewriteRequest("entrada qualquer")));

        Assert.Equal(IntentRewriteFailureReason.InvalidResponse, ex.Reason);
    }

    [Fact]
    public async Task RewriteAsync_when_content_filtered_throws_ContentFiltered()
    {
        var responseJson = BuildChatCompletionResponseWithRawContent(string.Empty, "content_filter", 10, 10);

        var rewriter = CreateRewriter(responseJson);

        var ex = await Assert.ThrowsAsync<IntentRewriteException>(
            () => rewriter.RewriteAsync(new IntentRewriteRequest("entrada qualquer")));

        Assert.Equal(IntentRewriteFailureReason.ContentFiltered, ex.Reason);
    }

    [Fact]
    public async Task RewriteAsync_with_long_fact_rich_input_and_full_length_response_throws_OutputTruncated()
    {
        var manyAttributeHints = string.Join(",", Enumerable.Range(0, 40)
            .Select(i => $$"""{ "rawName": "atributo{{i}}", "rawValue": "valor{{i}}" }"""));

        var partialJson = $$"""{"normalizedQuery":"produto com muitos atributos","attributeHints":[{{manyAttributeHints}}""";

        var responseJson = BuildChatCompletionResponseWithRawContent(partialJson, "length", 2000, 500);

        var rewriter = CreateRewriter(responseJson);

        var longInput = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"fato{i} valor{i}"));

        var ex = await Assert.ThrowsAsync<IntentRewriteException>(
            () => rewriter.RewriteAsync(new IntentRewriteRequest(longInput)));

        Assert.Equal(IntentRewriteFailureReason.OutputTruncated, ex.Reason);
    }

    private static string BuildChatCompletionResponseWithRawContent(
        string rawContent,
        string finishReason,
        int completionTokens,
        int promptTokens)
    {
        var serializedContent = System.Text.Json.JsonSerializer.Serialize(rawContent);

        return $$"""
            {
              "id": "chatcmpl-test",
              "object": "chat.completion",
              "created": 1700000000,
              "model": "fake-intent-rewriter",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": {{serializedContent}}
                  },
                  "finish_reason": "{{finishReason}}"
                }
              ],
              "usage": {
                "prompt_tokens": {{promptTokens}},
                "completion_tokens": {{completionTokens}},
                "total_tokens": {{promptTokens + completionTokens}}
              }
            }
            """;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string responseJson, HttpStatusCode statusCode)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
