using Yunu.Commerce.Catalog.Application.GoogleTaxonomy;
using Yunu.Commerce.Catalog.Application.GoogleTaxonomy.SynchronizeGoogleTaxonomy;

namespace Yunu.Commerce.Api.GoogleTaxonomy;

/// <summary>
/// Maps the Catalog Google Product Taxonomy HTTP endpoints
/// (docs task: "Implement the complete Google Product Taxonomy import/synchronization
/// feature"). This class only translates HTTP input/output to/from existing
/// Application commands, queries and handlers. It contains no business logic,
/// no repository logic, and never touches SQL Server directly.
///
/// The synchronize endpoint is administrative (not public commerce functionality)
/// and is grouped under /api/admin so authorization can be layered on later.
/// </summary>
public static class CatalogGoogleTaxonomyEndpoints
{
    public static IEndpointRouteBuilder MapCatalogGoogleTaxonomyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/catalog/google-taxonomy/synchronize", SynchronizeAsync)
            .Produces<SynchronizeGoogleTaxonomyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/google-taxonomy/{googleCategoryId:int}", GetByIdAsync)
            .Produces<GoogleTaxonomyCategoryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/catalog/google-taxonomy/search", SearchAsync)
            .Produces<IReadOnlyCollection<GoogleTaxonomyCategoryResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/google-taxonomy/{googleCategoryId:int}/ancestors", GetAncestorsAsync)
            .Produces<IReadOnlyCollection<GoogleTaxonomyAncestorResponse>>(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> SynchronizeAsync(
        SynchronizeGoogleTaxonomyHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(new SynchronizeGoogleTaxonomyCommand(), cancellationToken);

            var response = new SynchronizeGoogleTaxonomyResponse
            {
                Status = result.Status,
                TotalCategories = result.TotalCategories,
                Inserted = result.Inserted,
                Updated = result.Updated,
                Deactivated = result.Deactivated,
                StartedAt = result.StartedAtUtc,
                CompletedAt = result.CompletedAtUtc
            };

            return Results.Ok(response);
        }
        catch (GoogleTaxonomySynchronizationInProgressException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (GoogleTaxonomyValidationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetByIdAsync(
        int googleCategoryId,
        IGoogleTaxonomyRepository repository,
        CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(googleCategoryId, cancellationToken);

        return category is null ? Results.NotFound() : Results.Ok(category);
    }

    private static async Task<IResult> SearchAsync(
        string query,
        int? limit,
        IGoogleTaxonomyRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.Problem(detail: "'query' must not be empty.", statusCode: StatusCodes.Status400BadRequest);
        }

        var results = await repository.SearchAsync(query, limit ?? 20, cancellationToken);

        return Results.Ok(results);
    }

    private static async Task<IResult> GetAncestorsAsync(
        int googleCategoryId,
        IGoogleTaxonomyRepository repository,
        CancellationToken cancellationToken)
    {
        var ancestors = await repository.GetAncestorsAsync(googleCategoryId, cancellationToken);

        var response = ancestors
            .Select(a => new GoogleTaxonomyAncestorResponse
            {
                GoogleCategoryId = a.GoogleCategoryId,
                Name = a.Name,
                Level = a.Level
            })
            .ToArray();

        return Results.Ok(response);
    }
}
