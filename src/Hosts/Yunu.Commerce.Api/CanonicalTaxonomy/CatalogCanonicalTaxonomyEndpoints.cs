using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyChildren;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyNodeById;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.GetCanonicalTaxonomyRoots;

namespace Yunu.Commerce.Api.CanonicalTaxonomy;

/// <summary>
/// Maps the read-only Canonical Taxonomy HTTP endpoints (docs task: "CQRS de
/// leitura e endpoints GET para Segments e Canonical Taxonomy" §4). This
/// class only translates HTTP input/output to/from existing Application
/// queries and handlers. It contains no business logic, no repository logic,
/// and never touches SQL Server directly.
///
/// Create/Update/Delete of Canonical Taxonomy nodes are intentionally not
/// exposed here: no POST, PUT, PATCH or DELETE endpoint is mapped.
/// </summary>
public static class CatalogCanonicalTaxonomyEndpoints
{
    public static IEndpointRouteBuilder MapCatalogCanonicalTaxonomyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/canonical-taxonomy/roots", GetRootsAsync)
            .Produces<IReadOnlyCollection<CanonicalTaxonomyNodeResponse>>(StatusCodes.Status200OK);

        app.MapGet("/api/catalog/canonical-taxonomy/{canonicalTaxonomyNodeId:long}", GetByIdAsync)
            .Produces<CanonicalTaxonomyNodeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/canonical-taxonomy/{canonicalTaxonomyNodeId:long}/children", GetChildrenAsync)
            .Produces<IReadOnlyCollection<CanonicalTaxonomyNodeResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetRootsAsync(
        GetCanonicalTaxonomyRootsHandler handler,
        CancellationToken cancellationToken)
    {
        var roots = await handler.HandleAsync(new GetCanonicalTaxonomyRootsQuery(), cancellationToken);

        return Results.Ok(roots);
    }

    private static async Task<IResult> GetByIdAsync(
        long canonicalTaxonomyNodeId,
        GetCanonicalTaxonomyNodeByIdHandler handler,
        CancellationToken cancellationToken)
    {
        if (canonicalTaxonomyNodeId <= 0)
        {
            return Results.Problem(
                detail: "canonicalTaxonomyNodeId must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var query = new GetCanonicalTaxonomyNodeByIdQuery { CanonicalTaxonomyNodeId = canonicalTaxonomyNodeId };
        var node = await handler.HandleAsync(query, cancellationToken);

        return node is null ? Results.NotFound() : Results.Ok(node);
    }

    private static async Task<IResult> GetChildrenAsync(
        long canonicalTaxonomyNodeId,
        GetCanonicalTaxonomyNodeByIdHandler nodeHandler,
        GetCanonicalTaxonomyChildrenHandler childrenHandler,
        CancellationToken cancellationToken)
    {
        if (canonicalTaxonomyNodeId <= 0)
        {
            return Results.Problem(
                detail: "canonicalTaxonomyNodeId must be greater than zero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var nodeQuery = new GetCanonicalTaxonomyNodeByIdQuery { CanonicalTaxonomyNodeId = canonicalTaxonomyNodeId };
        var parent = await nodeHandler.HandleAsync(nodeQuery, cancellationToken);

        if (parent is null)
        {
            return Results.NotFound();
        }

        var childrenQuery = new GetCanonicalTaxonomyChildrenQuery { ParentId = canonicalTaxonomyNodeId };
        var children = await childrenHandler.HandleAsync(childrenQuery, cancellationToken);

        return Results.Ok(children);
    }
}
