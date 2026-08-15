using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Api.AI.AttributeResolution;
using Yunu.Commerce.Api.AI.CategoryResolution;
using Yunu.Commerce.Api.AI.IntentRewriting;
using Yunu.Commerce.Catalog.Application.CatalogIntentResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;

namespace Yunu.Commerce.Api.AI.CatalogIntentResolution;

/// <summary>
/// Maps the end-to-end catalog intent resolution HTTP endpoint (docs task:
/// "Catalog intent resolution orchestration"): natural-language input →
/// Intent Rewriter (once) → Google Category Resolution → Attribute Hint
/// Resolution. This class only translates HTTP input/output to/from <see
/// cref="ICatalogIntentResolutionOrchestrator"/>; it never references Azure
/// OpenAI, Npgsql, SqlClient or any vendor-specific type. Resolution-only:
/// never creates Product/Sku, never persists anything, never publishes
/// events.
/// </summary>
public static class CatalogIntentResolutionEndpoints
{
    private const int MaxInputLength = 2000;

    public static IEndpointRouteBuilder MapCatalogIntentResolutionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/catalog/resolve", ResolveAsync)
            .WithSummary("Interpret and resolve a natural-language catalog creation request end to end")
            .WithDescription("Runs the Intent Rewriter once, then resolves the Google category and attribute hints deterministically (embeddings + pgvector + SQL Server validation). Read-only: never creates Product/Sku, never persists anything.")
            .Produces<CatalogIntentResolutionHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> ResolveAsync(
        CatalogIntentResolutionHttpRequest request,
        ICatalogIntentResolutionOrchestrator orchestrator,
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
            var result = await orchestrator.ResolveAsync(
                new Yunu.Commerce.Catalog.Application.CatalogIntentResolution.CatalogIntentResolutionRequest(request.Input, locale),
                cancellationToken);

            var response = new CatalogIntentResolutionHttpResponse
            {
                Status = result.Status.ToString(),
                Intent = result.Intent is null ? null : MapIntent(result.Intent),
                Category = result.Category is null ? null : MapCategory(result.Category),
                Attributes = result.Attributes is null ? null : MapAttributes(result.Attributes),
                ReadyForProposal = result.ReadyForProposal,
                Warnings = result.Warnings
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
        catch (CategoryResolutionEmbeddingModelMismatchException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Yunu.Commerce.Catalog.Application.AttributeResolution.AttributeResolutionEmbeddingModelMismatchException ex)
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

    private static RewriteIntentResponse MapIntent(IntentRewriteResult intent) => new()
    {
        OriginalInput = intent.OriginalInput,
        NormalizedQuery = intent.NormalizedQuery,
        SemanticQuery = intent.SemanticQuery,
        Intent = intent.Intent.ToString(),
        DetectedLanguage = intent.DetectedLanguage,
        TargetLocale = intent.TargetLocale,
        CategoryHint = intent.CategoryHint,
        AttributeHints = intent.AttributeHints
            .Select(h => new AttributeHintResponse { RawName = h.RawName, RawValue = h.RawValue })
            .ToArray(),
        SearchTerms = intent.SearchTerms,
        Confidence = intent.Confidence
    };

    private static ResolveGoogleCategoryHttpResponse MapCategory(ResolveGoogleCategoryResult category) => new()
    {
        RawCategoryHint = category.RawCategoryHint,
        Status = category.Status.ToString(),
        GoogleCategoryId = category.GoogleCategoryId,
        CategoryName = category.CategoryName,
        CategoryPath = category.CategoryPath,
        Depth = category.Depth,
        Similarity = category.Similarity,
        Candidates = category.Candidates.Select(c => new GoogleCategoryCandidateDto
        {
            GoogleCategoryId = c.GoogleCategoryId,
            CategoryName = c.CategoryName,
            CategoryPath = c.CategoryPath,
            Depth = c.Depth,
            Similarity = c.Similarity
        }).ToArray(),
        Reason = category.Reason
    };

    private static ResolveAttributeHintsHttpResponse MapAttributes(
        Yunu.Commerce.Catalog.Application.AttributeResolution.ResolveAttributeHintsResult attributes) => new()
    {
        AllResolved = attributes.AllResolved,
        Attributes = attributes.Attributes.Select(a => new ResolvedAttributeHintDto
        {
            RawName = a.RawName,
            RawValue = a.RawValue,
            Status = a.Status.ToString(),
            AttributeDefinitionId = a.AttributeDefinitionId,
            AttributeCode = a.AttributeCode,
            AttributeName = a.AttributeName,
            DataType = a.DataType,
            NormalizedValue = a.NormalizedValue,
            AttributeOptionId = a.AttributeOptionId,
            OptionCode = a.OptionCode,
            OptionName = a.OptionName,
            DefinitionSimilarity = a.DefinitionSimilarity,
            ValueSimilarity = a.ValueSimilarity,
            RequirementLevel = a.RequirementLevel?.ToString(),
            Candidates = a.Candidates.Select(c => new AttributeCandidateDto
            {
                AttributeDefinitionId = c.AttributeDefinitionId,
                AttributeCode = c.AttributeCode,
                AttributeName = c.AttributeName,
                Similarity = c.Similarity
            }).ToArray(),
            OptionCandidates = a.OptionCandidates.Select(c => new AttributeOptionCandidateDto
            {
                AttributeOptionId = c.AttributeOptionId,
                OptionCode = c.OptionCode,
                OptionName = c.OptionName,
                Similarity = c.Similarity
            }).ToArray(),
            Reason = a.Reason
        }).ToArray()
    };
}
