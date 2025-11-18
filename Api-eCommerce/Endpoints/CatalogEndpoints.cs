using CC.Aplication.Catalog;
using CC.Aplication.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api_eCommerce.Endpoints
{
    public static class CatalogEndpoints
    {
        public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/catalog")
                .WithTags("Catalog");

            group.MapGet("/products", GetProducts)
                .WithName("GetProducts")
                .WithSummary("Obtiene la lista de productos")
                .Produces<List<ProductDto>>(StatusCodes.Status200OK);

            group.MapGet("/products/{id}", GetProductById)
                .WithName("GetProductById")
                .WithSummary("Obtiene un producto por ID")
                .Produces<ProductDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/products/search", SearchProducts)
                .WithName("SearchProducts")
                .WithSummary("Busca productos por nombre o descripción")
                .Produces<List<ProductDto>>(StatusCodes.Status200OK);

            group.MapPost("/products", CreateProduct)
                .WithName("CreateProduct")
                .WithSummary("Crea un nuevo producto (requiere autenticación)")
                .Produces<ProductDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest);

            return app;
        }

        private static async Task<IResult> GetProducts(
            [FromServices] ICatalogService catalogService,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var products = await catalogService.GetProductsAsync(page, pageSize);
            return Results.Ok(products);
        }

        private static async Task<IResult> GetProductById(
            Guid id,
            [FromServices] ICatalogService catalogService)
        {
            var product = await catalogService.GetProductByIdAsync(id);
            return product != null ? Results.Ok(product) : Results.NotFound();
        }

        private static async Task<IResult> SearchProducts(
            [FromQuery] string q,
            [FromServices] ICatalogService catalogService)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query parameter 'q' is required" });
            }

            var products = await catalogService.SearchProductsAsync(q);
            return Results.Ok(products);
        }

        private static async Task<IResult> CreateProduct(
            [FromBody] CC.Aplication.Catalog.CreateProductRequest request,  // Usar el correcto
            [FromServices] ICatalogService catalogService)
        {
            try
            {
                var product = await catalogService.CreateProductAsync(request);
                return Results.Created($"/api/catalog/products/{product.Id}", product);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }
    }
}
