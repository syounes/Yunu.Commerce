using Yunu.Commerce.Catalog.Application.Brands.CreateBrand;
using Yunu.Commerce.Catalog.Application.Brands.GetBrand;
using Yunu.Commerce.Catalog.Application.Brands.ResolveBrand;
using Yunu.Commerce.Catalog.Application.Brands.UpdateBrand;
using Yunu.Commerce.Catalog.Domain.Brands;

namespace Yunu.Commerce.Api.Brands;

public static class CatalogBrandEndpoints
{
    public static IEndpointRouteBuilder MapCatalogBrandEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/catalog/brands", CreateBrandAsync)
            .Produces<CreateBrandResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/catalog/brands/{brandId}", GetBrandByIdAsync)
            .Produces<BrandResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/catalog/brands/by-code/{code}", GetBrandByCodeAsync)
            .Produces<BrandResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPut("/api/catalog/brands/{brandId}", UpdateBrandAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateBrandAsync(
        CreateBrandRequest request,
        CreateBrandHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateBrandCommand
            {
                Code = request.Code,
                Name = request.Name
            };

            var result = await handler.HandleAsync(command, cancellationToken);

            var response = new CreateBrandResponse { BrandId = result.BrandId };

            return Results.Created($"/api/catalog/brands/{result.BrandId}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<IResult> GetBrandByIdAsync(
        string brandId,
        GetBrandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(brandId, out var parsed))
        {
            return Results.Problem(detail: $"'{brandId}' is not a valid Brand identifier.", statusCode: StatusCodes.Status400BadRequest);
        }

        var query = new GetBrandQuery { BrandId = new BrandId(parsed) };
        var brand = await handler.Handle(query, cancellationToken);

        return brand is null
            ? Results.NotFound()
            : Results.Ok(new BrandResponse
            {
                BrandId = brand.Id.Value,
                Code = brand.Code.Value,
                Name = brand.Name.Value,
                NormalizedName = brand.NormalizedName,
                Status = brand.Status.ToString()
            });
    }

    private static async Task<IResult> GetBrandByCodeAsync(
        string code,
        IBrandResolver resolver,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate code format by attempting to construct BrandCode
            var _ = new BrandCode(code);
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var resolved = await resolver.ResolveAsync(code, cancellationToken);
        return resolved is null ? Results.NotFound() : Results.Ok(new BrandResponse
        {
            BrandId = resolved.Id.Value,
            Code = resolved.Code.Value,
            Name = resolved.Name.Value,
            NormalizedName = resolved.NormalizedName,
            Status = resolved.Status.ToString()
        });
    }

    private static async Task<IResult> UpdateBrandAsync(
        string brandId,
        UpdateBrandRequest request,
        UpdateBrandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(brandId, out var parsed))
        {
            return Results.Problem(detail: $"'{brandId}' is not a valid Brand identifier.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var command = new UpdateBrandCommand
            {
                BrandId = parsed,
                Name = request.Name,
                Status = request.Status
            };

            await handler.HandleAsync(command, cancellationToken);

            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
