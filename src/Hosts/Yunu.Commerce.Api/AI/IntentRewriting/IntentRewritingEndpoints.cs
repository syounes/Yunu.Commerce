using Yunu.Commerce.AI.Application.IntentRewriting;

namespace Yunu.Commerce.Api.AI.IntentRewriting;

/// <summary>
/// Maps the Intent/Query Rewriting HTTP endpoint (docs task: "Intent/Query
/// Rewriting"). This class only translates HTTP input/output to/from the
/// AI.Application <see cref="IIntentRewriter"/> port; it never references
/// Azure OpenAI, the OpenAI SDK, endpoints or API keys directly. This is an
/// initial validation endpoint only: it does not connect to retrieval,
/// Product/Sku creation, or persistence.
/// </summary>
public static class IntentRewritingEndpoints
{
    private const int MaxInputLength = 2000;

    public static IEndpointRouteBuilder MapIntentRewritingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/intents/rewrite", RewriteIntentAsync)
            .WithSummary("Rewrite a natural-language catalog query/intent")
            .WithDescription("Normalizes, classifies and extracts textual hints from a natural-language catalog query using the configured Intent Rewriter model. Returns only textual hints; never official catalog identifiers.")
            .Produces<RewriteIntentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> RewriteIntentAsync(
        RewriteIntentRequest request,
        IIntentRewriter intentRewriter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return Results.Problem(
                detail: "Input cannot be null, empty or whitespace.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.Input.Length > MaxInputLength)
        {
            return Results.Problem(
                detail: $"Input cannot be longer than {MaxInputLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? "pt-BR" : request.Locale;

        try
        {
            var result = await intentRewriter.RewriteAsync(
                new IntentRewriteRequest(request.Input, locale),
                cancellationToken);

            var response = new RewriteIntentResponse
            {
                OriginalInput = result.OriginalInput,
                NormalizedQuery = result.NormalizedQuery,
                SemanticQuery = result.SemanticQuery,
                Intent = result.Intent.ToString(),
                DetectedLanguage = result.DetectedLanguage,
                TargetLocale = result.TargetLocale,
                CategoryHint = result.CategoryHint,
                AttributeHints = result.AttributeHints
                    .Select(h => new AttributeHintResponse { Name = h.Name, Value = h.Value })
                    .ToArray(),
                SearchTerms = result.SearchTerms,
                Confidence = result.Confidence
            };

            return Results.Ok(response);
        }
        catch (IntentRewriteException ex)
        {
            return ex.Reason switch
            {
                IntentRewriteFailureReason.ContentFiltered => Results.Problem(
                    detail: "The request could not be processed because it was blocked by the content filter.",
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                IntentRewriteFailureReason.Timeout or IntentRewriteFailureReason.ProviderUnavailable
                    or IntentRewriteFailureReason.RateLimited => Results.Problem(
                        detail: "The intent rewriting provider is currently unavailable. Please try again later.",
                        statusCode: StatusCodes.Status503ServiceUnavailable),
                _ => Results.Problem(
                    detail: "The intent rewriting provider returned an unexpected response.",
                    statusCode: StatusCodes.Status503ServiceUnavailable)
            };
        }
    }
}
