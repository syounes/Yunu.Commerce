using Yunu.Commerce.Catalog.Application.Products.GetProductById;
using Yunu.Commerce.Catalog.Application.Products.TransitionProductStatus;
using Yunu.Commerce.Catalog.Domain.Products;

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
/// Only semantic lifecycle actions are exposed (docs task: "V11 - Product/Sku
/// Lifecycle Concurrency + API Governance"): deactivate/reactivate/archive.
/// No generic "set Status to X" endpoint exists, so HTTP can never send an
/// arbitrary target Status. In particular:
/// - There is no public "activate" endpoint: the initial Draft -&gt; Active
///   transition remains internal/governed (ProductProposal materialization).
/// - "reactivate" only ever means Inactive -&gt; Active.
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

        app.MapPost("/api/catalog/products/{productId}/deactivate", (string productId, TransitionProductStatusHandler handler, CancellationToken cancellationToken) =>
                TransitionStatusAsync(productId, ProductStatus.Inactive, handler, cancellationToken))
            .WithSummary("Deactivate a Product")
            .WithDescription("Transitions the Product to Inactive (docs/adr/0012). Only Active -> Inactive is a valid source state.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/catalog/products/{productId}/reactivate", (string productId, TransitionProductStatusHandler handler, CancellationToken cancellationToken) =>
                TransitionStatusAsync(productId, ProductStatus.Active, handler, cancellationToken))
            .WithSummary("Reactivate a Product")
            .WithDescription("Transitions the Product from Inactive back to Active (docs/adr/0012). A Draft Product cannot be activated through this endpoint; the initial Draft -> Active transition is internal/governed.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/catalog/products/{productId}/archive", (string productId, TransitionProductStatusHandler handler, CancellationToken cancellationToken) =>
                TransitionStatusAsync(productId, ProductStatus.Archived, handler, cancellationToken))
            .WithSummary("Archive a Product")
            .WithDescription("Transitions the Product to Archived, a terminal state (docs/adr/0012). Blocked while the Product still has a non-Archived Sku, including under concurrent Sku creation/(re)activation.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> TransitionStatusAsync(
        string productId,
        ProductStatus targetStatus,
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
                Status = targetStatus.ToString()
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
        catch (Yunu.Commerce.Catalog.Application.Products.ProductStatusConcurrencyConflictException ex)
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

