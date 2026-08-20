using Yunu.Commerce.Catalog.Application.Skus.GetSkuById;
using Yunu.Commerce.Catalog.Application.Skus.GetSkusByProductId;
using Yunu.Commerce.Catalog.Application.Skus.TransitionSkuStatus;
using Yunu.Commerce.Catalog.Domain.Skus;

namespace Yunu.Commerce.Api.Skus;

/// <summary>
/// Maps the Catalog Sku HTTP endpoints. Sku is now an independent Aggregate Root
/// (docs/adr/0010-separate-product-and-sku-aggregate-boundaries.md).
///
/// Direct public creation of a Sku is intentionally not exposed here
/// (docs/adr/0012-governed-product-and-sku-mutation-and-commercial-eligibility.md):
/// materialized Skus are only created through the governed ProductProposal
/// conversion flow. <see cref="Yunu.Commerce.Catalog.Application.Skus.CreateSku.CreateSkuHandler"/>
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
/// This class only translates HTTP input/output to/from existing Application commands, queries
/// and handlers. It contains no Domain rules, no repository logic, and never
/// instantiates Sku or touches MongoDB directly.
/// </summary>
public static class CatalogSkuEndpoints
{
    public static IEndpointRouteBuilder MapCatalogSkuEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/products/{productId}/skus", GetSkusByProductIdAsync)
            .Produces<IReadOnlyCollection<SkuDetailsResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/skus/{skuId}", GetSkuByIdAsync)
            .Produces<SkuDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/api/catalog/skus/{skuId}/deactivate", (string skuId, TransitionSkuStatusHandler handler, CancellationToken cancellationToken) =>
                TransitionStatusAsync(skuId, SkuStatus.Inactive, handler, cancellationToken))
            .WithSummary("Deactivate a Sku")
            .WithDescription("Transitions the Sku to Inactive (docs/adr/0012). Only Active -> Inactive is a valid source state; a Draft Sku cannot be deactivated. Blocked while the owning Product is Archived.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/catalog/skus/{skuId}/reactivate", (string skuId, TransitionSkuStatusHandler handler, CancellationToken cancellationToken) =>
                TransitionStatusAsync(skuId, SkuStatus.Active, handler, cancellationToken))
            .WithSummary("Reactivate a Sku")
            .WithDescription("Transitions the Sku from Inactive back to Active (docs/adr/0012). A Draft Sku cannot be activated through this endpoint; the initial Draft -> Active transition is internal/governed. Blocked while the owning Product is Archived, including under concurrent Product Archive.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/catalog/skus/{skuId}/archive", (string skuId, TransitionSkuStatusHandler handler, CancellationToken cancellationToken) =>
                TransitionStatusAsync(skuId, SkuStatus.Archived, handler, cancellationToken))
            .WithSummary("Archive a Sku")
            .WithDescription("Transitions the Sku to Archived, a terminal state (docs/adr/0012). Always allowed regardless of the owning Product's Status.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> TransitionStatusAsync(
        string skuId,
        SkuStatus targetStatus,
        TransitionSkuStatusHandler handler,
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
            var command = new TransitionSkuStatusCommand
            {
                SkuId = parsedSkuId,
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
        catch (Yunu.Commerce.Catalog.Domain.Skus.InvalidSkuStatusTransitionException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Yunu.Commerce.Catalog.Application.Skus.ProductArchivedException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (Yunu.Commerce.Catalog.Application.Skus.SkuStatusConcurrencyConflictException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
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

