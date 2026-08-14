using Yunu.Commerce.AI.Application.Embeddings;

namespace Yunu.Commerce.Api.AI;

/// <summary>
/// Maps the AI Embeddings HTTP endpoints (docs task: "AI Embeddings smoke test").
/// This class only translates HTTP input/output to/from the AI.Application
/// <see cref="EmbeddingOrchestrator"/>. It never references Azure OpenAI, the
/// OpenAI SDK, endpoints or API keys directly.
/// </summary>
public static class AIEmbeddingsEndpoints
{
    public static IEndpointRouteBuilder MapAIEmbeddingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/embeddings/google-category", GenerateGoogleCategoryEmbeddingAsync)
            .WithSummary("Generate Google category embedding")
            .WithDescription("Generates a semantic embedding for a Google taxonomy hierarchy using the requested (or default) embedding provider.")
            .Produces<GenerateGoogleCategoryEmbeddingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GenerateGoogleCategoryEmbeddingAsync(
        GenerateGoogleCategoryEmbeddingRequest request,
        EmbeddingOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.Problem(
                detail: "Text cannot be null, empty or whitespace.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await orchestrator.GenerateAsync(request.Text, request.Provider, cancellationToken);

            var response = new GenerateGoogleCategoryEmbeddingResponse
            {
                Provider = result.Provider,
                Model = result.Model,
                Dimensions = result.Dimensions,
                Embedding = result.Embedding
            };

            return Results.Ok(response);
        }
        catch (UnknownEmbeddingProviderException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (EmbeddingGenerationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
