using Yunu.Commerce.Catalog.Application.AttributeEmbeddings;

namespace Yunu.Commerce.Api.AttributeEmbeddings;

/// <summary>
/// Maps the Catalog SKU attribute embedding synchronization HTTP endpoint
/// (docs task: "SKU attribute embedding synchronization pipeline"). This class
/// only translates HTTP input/output to/from the existing Application
/// command/handler. It contains no SQL, no PostgreSQL, no Azure calls, no
/// semantic text construction and no business logic. Mirrors
/// <see cref="Yunu.Commerce.Api.GoogleTaxonomy.CatalogGoogleTaxonomyEndpoints"/>.
///
/// The synchronize endpoint is administrative (not public commerce
/// functionality) and is grouped under /api/admin so authorization can be
/// layered on later.
/// </summary>
public static class CatalogAttributeEmbeddingEndpoints
{
    public static IEndpointRouteBuilder MapCatalogAttributeEmbeddingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/catalog/attribute-embeddings/synchronize", SynchronizeAsync)
            .WithSummary("Synchronize the pgvector projection of the active SKU attribute catalog")
            .WithDescription("Reads active searchable Attribute Definitions and active Attribute Options from SQL Server, skips entries already up-to-date in PostgreSQL + pgvector, and generates/persists embeddings for the rest in limited-concurrency batches via the AI module.")
            .Produces<SynchronizeAttributeEmbeddingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> SynchronizeAsync(
        SynchronizeAttributeEmbeddingsRequest? request,
        SynchronizeAttributeEmbeddingsHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new SynchronizeAttributeEmbeddingsCommand(
                request?.Provider,
                request?.BatchSize);

            var result = await handler.HandleAsync(command, cancellationToken);

            var response = new SynchronizeAttributeEmbeddingsResponse
            {
                Provider = result.Provider,
                Model = result.Model,
                DefinitionsRead = result.DefinitionsRead,
                OptionsRead = result.OptionsRead,
                Generated = result.Generated,
                Updated = result.Updated,
                Skipped = result.Skipped,
                Deactivated = result.Deactivated,
                Failed = result.Failed,
                StartedAtUtc = result.StartedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                DurationMilliseconds = result.DurationMilliseconds
            };

            return Results.Ok(response);
        }
        catch (AttributeEmbeddingSynchronizationInProgressException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
