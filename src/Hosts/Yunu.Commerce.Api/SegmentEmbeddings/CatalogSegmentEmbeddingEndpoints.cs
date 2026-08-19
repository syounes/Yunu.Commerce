using Yunu.Commerce.Catalog.Application.SegmentEmbeddings;

namespace Yunu.Commerce.Api.SegmentEmbeddings;

/// <summary>
/// Maps the Catalog Segment embedding synchronization HTTP endpoint (docs
/// task: "Implementar sincronização de embeddings de segmentos"). This class
/// only translates HTTP input/output to/from the existing Application
/// command/handler. It contains no SQL, no PostgreSQL, no Azure calls, no
/// semantic text construction and no business logic. Mirrors
/// <see cref="Yunu.Commerce.Api.AttributeEmbeddings.CatalogAttributeEmbeddingEndpoints"/>.
///
/// The synchronize endpoint is administrative (not public commerce
/// functionality) and is grouped under /api/admin so authorization can be
/// layered on later.
/// </summary>
public static class CatalogSegmentEmbeddingEndpoints
{
    public static IEndpointRouteBuilder MapCatalogSegmentEmbeddingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/catalog/segment-embeddings/synchronize", SynchronizeAsync)
            .WithSummary("Synchronize the pgvector projection of the active Segment catalog")
            .WithDescription("Reads active Segment Definitions and active Segment Options from SQL Server, upserts every active source into PostgreSQL + pgvector, deactivates projections no longer active, and generates/persists embeddings for pending rows in limited-concurrency batches via the AI module.")
            .Produces<SynchronizeSegmentEmbeddingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> SynchronizeAsync(
        SynchronizeSegmentEmbeddingsRequest? request,
        SynchronizeSegmentEmbeddingsHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new SynchronizeSegmentEmbeddingsCommand(
                request?.Provider,
                request?.BatchSize);

            var result = await handler.HandleAsync(command, cancellationToken);

            var response = new SynchronizeSegmentEmbeddingsResponse
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
        catch (SegmentEmbeddingSynchronizationInProgressException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
