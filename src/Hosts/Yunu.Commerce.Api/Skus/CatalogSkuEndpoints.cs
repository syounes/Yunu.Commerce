using Yunu.Commerce.Catalog.Application.Skus.CreateSku;
using Yunu.Commerce.Catalog.Application.Skus.GetSkuById;
using Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;

namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// Maps the Catalog Sku HTTP endpoints. Sku is now an independent Aggregate Root
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md). This class
/// only translates HTTP input/output to/from existing Application commands, queries
/// and handlers. It contains no Domain rules, no repository logic, and never
/// instantiates Sku or touches MongoDB directly.
/// </summary>
public static class CatalogSkuEndpoints
{
    public static IEndpointRouteBuilder MapCatalogSkuEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/catalog/products/{productId}/skus", CreateSkuAsync)
            .Produces<CreateSkuResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/products/{productId}/skus", GetSkusByProductIdAsync)
            .Produces<IReadOnlyCollection<SkuDetailsResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/skus/{skuId}", GetSkuByIdAsync)
            .Produces<SkuDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateSkuAsync(
        string productId,
        CreateSkuRequest request,
        CreateSkuHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(productId, out var parsedProductId))
        {
            return Results.Problem(
                detail: $"'{productId}' is not a valid Product identifier.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var command = new CreateSkuCommand
            {
                ProductId = parsedProductId,
                Code = request.Code,
                Gtin = request.Gtin
            };

            var result = await handler.HandleAsync(command, cancellationToken);

            var response = new CreateSkuResponse
            {
                SkuId = result.SkuId
            };

            return Results.Created($"/api/catalog/skus/{result.SkuId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Product") && ex.Message.Contains("does not exist"))
        {
            return Results.NotFound(new { detail = ex.Message });
        }
    }

    private static async Task<IResult> GetSkusByProductIdAsync(
        string productId,
        GetSkusByProductIdHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(productId, out var parsedProductId))
        {
            return Results.Problem(
                detail: $"'{productId}' is not a valid Product identifier.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var query = new GetSkusByProductIdQuery { ProductId = parsedProductId };
            var response = await handler.HandleAsync(query, cancellationToken);

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetSkuByIdAsync(
        string skuId,
        GetSkuByIdHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(skuId, out var parsedSkuId))
        {
            return Results.Problem(
                detail: $"'{skuId}' is not a valid Sku identifier.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var query = new GetSkuByIdQuery { SkuId = parsedSkuId };
            var response = await handler.HandleAsync(query, cancellationToken);

            return response is null
                ? Results.NotFound()
                : Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
