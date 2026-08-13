using Yunu.Commerce.Catalog.Application.Products.CreateProduct;
using Yunu.Commerce.Catalog.Application.Products.GetProductById;

namespace Yunu.Commerce.Api.Products;

/// <summary>
/// Maps the Catalog Product HTTP endpoints (docs/architecture/06-solution-structure.md §50).
/// This class only translates HTTP input/output to/from existing Application
/// commands, queries and handlers. It contains no Domain rules, no repository
/// logic, and never instantiates Product or touches MongoDB directly.
/// </summary>
public static class CatalogProductEndpoints
{
    public static IEndpointRouteBuilder MapCatalogProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/catalog/products", CreateProductAsync)
            .Produces<CreateProductResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/products/{productId}", GetProductByIdAsync)
            .Produces<ProductResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateProductAsync(
        CreateProductRequest request,
        CreateProductHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateProductCommand
            {
                Name = request.Name,
                Description = request.Description,
                BrandId = request.BrandId,
                CategoryId = request.CategoryId
            };

            var result = await handler.HandleAsync(command, cancellationToken);

            var response = new CreateProductResponse
            {
                ProductId = result.ProductId
            };

            return Results.Created($"/api/catalog/products/{result.ProductId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
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
