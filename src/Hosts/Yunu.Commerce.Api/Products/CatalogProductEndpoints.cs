using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Application.Products.TransitionProductStatus;

namespace Yunu.Commerce.Api.Products;

/// <summary>
/// Maps the Catalog Product HTTP endpoints (docs/architecture/06-solution-structure.md §50).
///
/// Direct public creation of a Product is intentionally not exposed here
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md):
/// materialized Products are only created through the governed ProductProposal
/// conversion flow. <see cref="Yunu.Commerce.Catalog.Application.Products.CreateProduct.CreateProductHandler"/>
/// remains an internal Application service consumed by that flow, not by public HTTP.
///
/// This class only translates HTTP input/output to/from existing Application
/// commands, queries and handlers. It contains no Domain rules, no repository
/// logic, and never instantiates Product or touches MongoDB directly.
/// </summary>
public static class CatalogProductEndpoints
{
    public static IEndpointRouteBuilder MapCatalogProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/products/{productId}", GetProductByIdAsync)
            .Produces<ProductResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/api/catalog/products/{productId}/status", TransitionStatusAsync)
            .WithSummary("Transition a Product's lifecycle Status")
            .WithDescription("Applies an explicit Draft/Active/Inactive/Archived lifecycle transition (docs/adr/0012). Archiving is blocked while the Product still has a non-Archived Sku.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> TransitionStatusAsync(
        string productId,
        TransitionProductStatusRequest request,
        TransitionProductStatusHandler handler,
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
            var command = new TransitionProductStatusCommand
            {
                ProductId = parsedProductId,
                Status = request.Status
            };

            await handler.HandleAsync(command, cancellationToken);

            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { detail = ex.Message });
        }
        catch (Yunu.Commerce.Catalog.Domain.Products.InvalidProductStatusTransitionException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Yunu.Commerce.Catalog.Application.Products.ProductHasNonArchivedSkusException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> GetProductByIdAsync(
        string productId,
        GetProductByIdHandler handler,
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
            var query = new GetProductByIdQuery { ProductId = parsedProductId };
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

/// <summary>
/// HTTP request body for the Product lifecycle Status transition endpoint
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md).
/// </summary>
public sealed class TransitionProductStatusRequest
{
    public required string Status { get; init; }
}
