// See https://aka.ms/new-console-template for more information
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using OpenAI;
using OpenAI.Chat;

var responseJson = """
{
  "id": "chatcmpl-test",
  "object": "chat.completion",
  "created": 1700000000,
  "model": "gpt-4o",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "{\"normalizedQuery\":\"abc\"}"
      },
      "finish_reason": "length"
    }
  ],
  "usage": {
    "prompt_tokens": 123,
    "completion_tokens": 2000,
    "total_tokens": 2123
  }
}
""";

var handler = new FakeHandler(responseJson);
var httpClient = new HttpClient(handler);
var transport = new HttpClientPipelineTransport(httpClient);

var options = new OpenAIClientOptions
{
    Endpoint = new Uri("https://example.invalid/openai/v1/"),
    Transport = transport
};

var client = new OpenAIClient(new ApiKeyCredential("fake-key"), options);
var chatClient = client.GetChatClient("fake-deployment");

var messages = new ChatMessage[] { new UserChatMessage("hi") };
var completionOptions = new ChatCompletionOptions { MaxOutputTokenCount = 2000 };

var result = await chatClient.CompleteChatAsync(messages, completionOptions);
var completion = result.Value;

Console.WriteLine($"FinishReason: {completion.FinishReason}");
Console.WriteLine($"Usage.InputTokenCount: {completion.Usage?.InputTokenCount}");
Console.WriteLine($"Usage.OutputTokenCount: {completion.Usage?.OutputTokenCount}");
Console.WriteLine($"Content.Count: {completion.Content.Count}");
if (completion.Content.Count > 0)
{
    Console.WriteLine($"Content[0].Text: {completion.Content[0].Text}");
}

sealed class FakeHandler : HttpMessageHandler
{
    private readonly string _json;
    public FakeHandler(string json) => _json = json;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}


