using Yunu.Commerce.Catalog.Application.SegmentCatalog;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionByCode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitionById;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentDefinitions;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionByCode;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionById;
using Yunu.Commerce.Catalog.Application.SegmentCatalog.GetSegmentOptionsByDefinition;

namespace Yunu.Commerce.Api.Segments;

/// <summary>
/// Maps the read-only Catalog Segments HTTP endpoints (docs task: "CQRS de
/// leitura e endpoints GET para Segments e Canonical Taxonomy" §2). This
/// class only translates HTTP input/output to/from existing Application
/// queries and handlers. It contains no business logic, no repository logic,
/// and never touches SQL Server directly.
///
/// Segments are exposed as read-only reference data: no POST, PUT, PATCH or
/// DELETE endpoint is mapped here.
/// </summary>
public static class CatalogSegmentEndpoints
{
    public static IEndpointRouteBuilder MapCatalogSegmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/segments", GetDefinitionsAsync)
            .Produces<IReadOnlyCollection<SegmentDefinitionResponse>>(StatusCodes.Status200OK);

        app.MapGet("/api/catalog/segments/{segmentDefinitionId:long}", GetDefinitionByIdAsync)
            .Produces<SegmentDefinitionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/segments/by-code/{code}", GetDefinitionByCodeAsync)
            .Produces<SegmentDefinitionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/segments/{segmentDefinitionId:long}/options", GetOptionsByDefinitionAsync)
            .Produces<IReadOnlyCollection<SegmentOptionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/segments/{segmentDefinitionId:long}/options/{segmentOptionId:long}", GetOptionByIdAsync)
            .Produces<SegmentOptionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/segments/{segmentDefinitionId:long}/options/by-code/{optionCode}", GetOptionByCodeAsync)
            .Produces<SegmentOptionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetDefinitionsAsync(
        GetSegmentDefinitionsHandler handler,
        CancellationToken cancellationToken)
    {
        var definitions = await handler.HandleAsync(new GetSegmentDefinitionsQuery(), cancellationToken);

        return Results.Ok(definitions);
    }

    private static async Task<IResult> GetDefinitionByIdAsync(
        long segmentDefinitionId,
        GetSegmentDefinitionByIdHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = segmentDefinitionId };
            var definition = await handler.HandleAsync(query, cancellationToken);

            return definition is null ? Results.NotFound() : Results.Ok(definition);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetDefinitionByCodeAsync(
        string code,
        GetSegmentDefinitionByCodeHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetSegmentDefinitionByCodeQuery { Code = code };
            var definition = await handler.HandleAsync(query, cancellationToken);

            return definition is null ? Results.NotFound() : Results.Ok(definition);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetOptionsByDefinitionAsync(
        long segmentDefinitionId,
        GetSegmentDefinitionByIdHandler definitionHandler,
        GetSegmentOptionsByDefinitionHandler optionsHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            var definitionQuery = new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = segmentDefinitionId };
            var definition = await definitionHandler.HandleAsync(definitionQuery, cancellationToken);

            if (definition is null)
            {
                return Results.NotFound();
            }

            var optionsQuery = new GetSegmentOptionsByDefinitionQuery { SegmentDefinitionId = segmentDefinitionId };
            var options = await optionsHandler.HandleAsync(optionsQuery, cancellationToken);

            return Results.Ok(options);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetOptionByIdAsync(
        long segmentDefinitionId,
        long segmentOptionId,
        GetSegmentDefinitionByIdHandler definitionHandler,
        GetSegmentOptionByIdHandler optionHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            var definitionQuery = new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = segmentDefinitionId };
            var definition = await definitionHandler.HandleAsync(definitionQuery, cancellationToken);

            if (definition is null)
            {
                return Results.NotFound();
            }

            var optionQuery = new GetSegmentOptionByIdQuery
            {
                SegmentDefinitionId = segmentDefinitionId,
                SegmentOptionId = segmentOptionId
            };

            var option = await optionHandler.HandleAsync(optionQuery, cancellationToken);

            return option is null ? Results.NotFound() : Results.Ok(option);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetOptionByCodeAsync(
        long segmentDefinitionId,
        string optionCode,
        GetSegmentDefinitionByIdHandler definitionHandler,
        GetSegmentOptionByCodeHandler optionHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            var definitionQuery = new GetSegmentDefinitionByIdQuery { SegmentDefinitionId = segmentDefinitionId };
            var definition = await definitionHandler.HandleAsync(definitionQuery, cancellationToken);

            if (definition is null)
            {
                return Results.NotFound();
            }

            var optionQuery = new GetSegmentOptionByCodeQuery
            {
                SegmentDefinitionId = segmentDefinitionId,
                OptionCode = optionCode
            };

            var option = await optionHandler.HandleAsync(optionQuery, cancellationToken);

            return option is null ? Results.NotFound() : Results.Ok(option);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
