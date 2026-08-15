using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Api.AI.CategoryResolution;

/// <summary>
/// Maps the isolated Google Category Resolution HTTP endpoint (docs task:
/// "Google Category Resolution"). This class only translates HTTP
/// input/output to/from <see cref="IGoogleCategoryResolver"/>; it never
/// references Azure OpenAI, Npgsql, SqlClient or any vendor-specific type.
/// Resolution-only: never persists Product/Sku data. Exists so the Category
/// Resolver can be tested/calibrated independently of the end-to-end
/// "/api/ai/catalog/resolve" orchestration.
/// </summary>
public static class ResolveGoogleCategoryEndpoints
{
    private const int MaxHintLength = 300;
    private const int MaxSemanticQueryLength = 2000;

    public static IEndpointRouteBuilder MapResolveGoogleCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/categories/resolve", ResolveAsync)
            .WithSummary("Resolve a textual category hint into an official Google Product Taxonomy category")
            .WithDescription("Resolves a rawCategoryHint (typically produced by the Intent Rewriter) into a validated GoogleTaxonomyCategories reference using exact match, semantic search (pgvector) and SQL Server validation. Read-only: never persists Product/Sku data.")
            .Produces<ResolveGoogleCategoryHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> ResolveAsync(
        ResolveGoogleCategoryHttpRequest request,
        IGoogleCategoryResolver resolver,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawCategoryHint))
        {
            return Results.Problem(
                detail: "rawCategoryHint is required and must not be empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.RawCategoryHint.Length > MaxHintLength)
        {
            return Results.Problem(
                detail: $"rawCategoryHint cannot be longer than {MaxHintLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.SemanticQuery is { Length: > MaxSemanticQueryLength })
        {
            return Results.Problem(
                detail: $"semanticQuery cannot be longer than {MaxSemanticQueryLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? "pt-BR" : request.Locale;

        try
        {
            var result = await resolver.ResolveAsync(
                new ResolveGoogleCategoryRequest(request.RawCategoryHint, request.SemanticQuery, locale),
                cancellationToken);

            var response = new ResolveGoogleCategoryHttpResponse
            {
                RawCategoryHint = result.RawCategoryHint,
                Status = result.Status.ToString(),
                GoogleCategoryId = result.GoogleCategoryId,
                CategoryName = result.CategoryName,
                CategoryPath = result.CategoryPath,
                Depth = result.Depth,
                Similarity = result.Similarity,
                Candidates = result.Candidates.Select(c => new GoogleCategoryCandidateDto
                {
                    GoogleCategoryId = c.GoogleCategoryId,
                    CategoryName = c.CategoryName,
                    CategoryPath = c.CategoryPath,
                    Depth = c.Depth,
                    Similarity = c.Similarity
                }).ToArray(),
                Reason = result.Reason
            };

            return Results.Ok(response);
        }
        catch (CategoryResolutionEmbeddingModelMismatchException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Yunu.Commerce.AI.Application.Embeddings.EmbeddingGenerationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Yunu.Commerce.AI.Application.Configuration.AIModelResolutionException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
