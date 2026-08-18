using Yunu.Commerce.AI.Application.IntentRewriting;
using Yunu.Commerce.Catalog.Application.AttributeResolution;
using Yunu.Commerce.Catalog.Application.CategoryResolution;
using Yunu.Commerce.Catalog.Application.ProductProposals;

namespace Yunu.Commerce.Api.ProductProposals;

/// <summary>
/// Maps the ProductProposal HTTP endpoints (docs task: "Catalog intent
/// resolution orchestration" - proposal persistence). This class only
/// translates HTTP input/output to/from existing Application commands,
/// queries and handlers; it contains no Domain rules and never calls the
/// intent resolution pipeline directly (that happens exclusively inside
/// <see cref="CreateProductProposalHandler"/>, via
/// <see cref="Yunu.Commerce.Catalog.Application.CatalogIntentResolution.ICatalogIntentResolutionOrchestrator"/>
/// - never over HTTP).
/// </summary>
public static class ProductProposalEndpoints
{
    private const int MaxInputLength = 2000;

    public static IEndpointRouteBuilder MapProductProposalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/catalog/product-proposals", CreateAsync)
            .WithSummary("Create a ProductProposal from natural-language input")
            .WithDescription("Runs the existing catalog intent resolution pipeline once and, only when every readiness criterion is met, persists the outcome as a ProductProposal (AwaitingReview). Never creates a canonical Product/Sku.")
            .Produces<CreateProductProposalResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/catalog/product-proposals/{proposalId}", GetByIdAsync)
            .WithSummary("Retrieve a ProductProposal by identity")
            .Produces<GetProductProposalResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateProductProposalRequest request,
        CreateProductProposalHandler handler,
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
            var command = new CreateProductProposalCommand(request.Input, locale);

            var result = await handler.HandleAsync(command, cancellationToken);

            var response = new CreateProductProposalResponse
            {
                ProposalId = result.ProposalId,
                Status = result.Status,
                ReadyForReview = result.ReadyForReview,
                CreatedAtUtc = result.CreatedAtUtc
            };

            return Results.Created($"/api/catalog/product-proposals/{result.ProposalId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ProductProposalResolutionException ex)
        {
            var resolution = ex.Resolution;

            return Results.Problem(
                detail: "The catalog intent resolution outcome is not ready to be persisted as a ProductProposal.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                extensions: new Dictionary<string, object?>
                {
                    ["status"] = resolution.Status.ToString(),
                    ["readyForProposal"] = resolution.ReadyForProposal,
                    ["warnings"] = resolution.Warnings
                });
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

    private static async Task<IResult> GetByIdAsync(
        string proposalId,
        GetProductProposalByIdHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(proposalId, out var parsedProposalId))
        {
            return Results.Problem(
                detail: $"'{proposalId}' is not a valid ProductProposal identifier.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var query = new GetProductProposalByIdQuery(parsedProposalId);
        var result = await handler.HandleAsync(query, cancellationToken);

        if (result is null)
        {
            return Results.NotFound();
        }

        var response = new GetProductProposalResponse
        {
            ProposalId = result.ProposalId,
            Status = result.Status,
            Locale = result.Locale,
            Source = new ProposalSourceResponse
            {
                OriginalInput = result.Source.OriginalInput,
                NormalizedQuery = result.Source.NormalizedQuery,
                SemanticQuery = result.Source.SemanticQuery,
                Intent = result.Source.Intent,
                DetectedLanguage = result.Source.DetectedLanguage,
                TargetLocale = result.Source.TargetLocale
            },
            Product = new ProposedProductResponse
            {
                SuggestedName = result.Product.SuggestedName,
                Description = result.Product.Description,
                BrandId = result.Product.BrandId,
                GoogleCategory = new ProposedGoogleCategoryResponse
                {
                    GoogleCategoryId = result.Product.GoogleCategory.GoogleCategoryId,
                    Name = result.Product.GoogleCategory.Name,
                    Path = result.Product.GoogleCategory.Path,
                    Depth = result.Product.GoogleCategory.Depth,
                    ResolutionStrategy = result.Product.GoogleCategory.ResolutionStrategy,
                    Similarity = result.Product.GoogleCategory.Similarity,
                    RerankConfidence = result.Product.GoogleCategory.RerankConfidence
                }
            },
            Skus = result.Skus.Select(sku => new ProposedSkuResponse
            {
                Id = sku.Id,
                SuggestedCode = sku.SuggestedCode,
                Gtin = sku.Gtin,
                Attributes = sku.Attributes.Select(attribute => new ProposedSkuAttributeResponse
                {
                    AttributeDefinitionId = attribute.AttributeDefinitionId,
                    AttributeCode = attribute.AttributeCode,
                    AttributeName = attribute.AttributeName,
                    Sequence = attribute.Sequence,
                    DataType = attribute.DataType,
                    RawName = attribute.RawName,
                    RawValue = attribute.RawValue,
                    NormalizedValue = attribute.NormalizedValue,
                    TypedValue = attribute.TypedValue is null
                        ? null
                        : new ProposedTypedValueResponse
                        {
                            DisplayValue = attribute.TypedValue.DisplayValue,
                            TextValue = attribute.TypedValue.TextValue,
                            IntegerValue = attribute.TypedValue.IntegerValue,
                            DecimalValue = attribute.TypedValue.DecimalValue,
                            BooleanValue = attribute.TypedValue.BooleanValue,
                            DateTimeValue = attribute.TypedValue.DateTimeValue,
                            MoneyAmount = attribute.TypedValue.MoneyAmount,
                            CurrencyCode = attribute.TypedValue.CurrencyCode,
                            MeasurementValue = attribute.TypedValue.MeasurementValue,
                            UnitCode = attribute.TypedValue.UnitCode,
                            JsonValue = attribute.TypedValue.JsonValue
                        },
                    AttributeOptionId = attribute.AttributeOptionId,
                    OptionCode = attribute.OptionCode,
                    OptionName = attribute.OptionName,
                    DefinitionResolutionStrategy = attribute.DefinitionResolutionStrategy,
                    OptionResolutionStrategy = attribute.OptionResolutionStrategy,
                    DefinitionSimilarity = attribute.DefinitionSimilarity,
                    ValueSimilarity = attribute.ValueSimilarity,
                    DefinitionRerankConfidence = attribute.DefinitionRerankConfidence,
                    OptionRerankConfidence = attribute.OptionRerankConfidence
                }).ToArray()
            }).ToArray(),
            Resolution = new ProposalResolutionResponse
            {
                Status = result.Resolution.Status,
                CategoryResolved = result.Resolution.CategoryResolved,
                AllAttributesResolved = result.Resolution.AllAttributesResolved,
                ReadyForProposal = result.Resolution.ReadyForProposal,
                IntentConfidence = result.Resolution.IntentConfidence,
                Warnings = result.Resolution.Warnings
            },
            CreatedAtUtc = result.CreatedAtUtc,
            UpdatedAtUtc = result.UpdatedAtUtc,
            ConfirmedAtUtc = result.ConfirmedAtUtc,
            ConvertedAtUtc = result.ConvertedAtUtc,
            CreatedProductId = result.CreatedProductId
        };

        return Results.Ok(response);
    }
}
