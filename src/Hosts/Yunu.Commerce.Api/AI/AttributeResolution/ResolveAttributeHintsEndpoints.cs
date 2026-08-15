using Yunu.Commerce.Catalog.Application.AttributeResolution;

namespace Yunu.Commerce.Api.AI.AttributeResolution;

/// <summary>
/// Maps the semantic attribute hint resolution HTTP endpoint (docs task:
/// "Semantic attribute hint resolution"). This class only translates HTTP
/// input/output to/from <see cref="IAttributeHintResolver"/>; it never
/// references Azure OpenAI, Npgsql, SqlClient or any vendor-specific type.
/// Resolution-only: never persists Product/Sku/SkuAttributeValues.
/// </summary>
public static class ResolveAttributeHintsEndpoints
{
    private const int MaxHints = 20;
    private const int MaxRawNameLength = 200;
    private const int MaxRawValueLength = 500;

    public static IEndpointRouteBuilder MapResolveAttributeHintsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/attributes/resolve", ResolveAsync)
            .WithSummary("Resolve textual attribute hints into official catalog references")
            .WithDescription("Resolves rawName/rawValue hints (typically produced by the Intent Rewriter) into validated Catalog.AttributeDefinitions/AttributeOptions references using exact match, semantic search (pgvector) and SQL Server validation. Read-only: never persists Product/Sku/SkuAttributeValues.")
            .Produces<ResolveAttributeHintsHttpResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> ResolveAsync(
        ResolveAttributeHintsHttpRequest request,
        IAttributeHintResolver resolver,
        CancellationToken cancellationToken)
    {
        if (request.AttributeHints is null || request.AttributeHints.Count == 0)
        {
            return Results.Problem(
                detail: "attributeHints is required and must contain at least one item.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.AttributeHints.Count > MaxHints)
        {
            return Results.Problem(
                detail: $"attributeHints cannot contain more than {MaxHints} items.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        foreach (var hint in request.AttributeHints)
        {
            if (string.IsNullOrWhiteSpace(hint.RawName))
            {
                return Results.Problem(
                    detail: "Every attribute hint requires a non-empty rawName.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (hint.RawName.Length > MaxRawNameLength)
            {
                return Results.Problem(
                    detail: $"rawName cannot be longer than {MaxRawNameLength} characters.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (hint.RawValue is { Length: > MaxRawValueLength })
            {
                return Results.Problem(
                    detail: $"rawValue cannot be longer than {MaxRawValueLength} characters.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var duplicateNames = request.AttributeHints
            .GroupBy(h => h.RawName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (duplicateNames.Length > 0)
        {
            return Results.Problem(
                detail: $"Duplicate attribute hints are not allowed: {string.Join(", ", duplicateNames)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.GoogleCategoryId is <= 0)
        {
            return Results.Problem(
                detail: "googleCategoryId must be a positive number when provided.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? "pt-BR" : request.Locale;

        try
        {
            var applicationRequest = new ResolveAttributeHintsRequest(
                request.AttributeHints.Select(h => h.ToAttributeHint()).ToArray(),
                request.GoogleCategoryId,
                locale);

            var result = await resolver.ResolveAsync(applicationRequest, cancellationToken);

            var response = new ResolveAttributeHintsHttpResponse
            {
                AllResolved = result.AllResolved,
                Attributes = result.Attributes.Select(a => new ResolvedAttributeHintDto
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

            return Results.Ok(response);
        }
        catch (AttributeResolutionEmbeddingModelMismatchException ex)
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
