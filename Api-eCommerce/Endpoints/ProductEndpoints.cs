using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using Api_eCommerce.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Endpoints
{
  /// <summary>
  /// Endpoints para gestión de productos del catálogo
  /// Requiere autenticación y permisos sobre el módulo "catalog"
  /// </summary>
  public static class ProductEndpoints
  {
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
      var group = app.MapGroup("/api/admin/products")
          .WithTags("Products - Admin")
          .RequireAuthorization(); // Requiere autenticación

      // ==================== CRUD BÁSICO ====================

      // GET /api/admin/products - Listar productos con paginación y filtros
      group.MapGet("/", GetProducts)
          .WithName("AdminGetProducts")
          .WithSummary("Obtener lista paginada de productos")
          .WithDescription("Retorna productos con filtros opcionales (búsqueda, categoría, precio, stock, etc.)")
          .RequireModule("catalog", canView: true)
          .Produces<PagedResult<ProductResponseDto>>(200)
          .ProducesProblem(401)
          .ProducesProblem(403);

      // GET /api/admin/products/{id} - Obtener producto por ID
      group.MapGet("/{id:guid}", GetProductById)
          .WithName("AdminGetProductById")
          .WithSummary("Obtener producto por ID")
          .RequireModule("catalog", canView: true)
          .Produces<ProductResponseDto>(200)
          .Produces(404)
          .ProducesProblem(401);

      // GET /api/admin/products/slug/{slug} - Obtener producto por slug (SEO)
      group.MapGet("/slug/{slug}", GetProductBySlug)
          .WithName("AdminGetProductBySlug")
          .WithSummary("Obtener producto por slug (URL amigable)")
          .RequireModule("catalog", canView: true)
          .Produces<ProductResponseDto>(200)
          .Produces(404);

      // POST /api/admin/products - Crear nuevo producto
      group.MapPost("/", CreateProduct)
          .WithName("AdminCreateProduct")
          .WithSummary("Crear nuevo producto")
          .RequireModule("catalog", canCreate: true)
          .Produces<ProductResponseDto>(201)
          .ProducesValidationProblem()
          .ProducesProblem(401)
          .ProducesProblem(403);

      // PUT /api/admin/products/{id} - Actualizar producto
      group.MapPut("/{id:guid}", UpdateProduct)
          .WithName("AdminUpdateProduct")
          .WithSummary("Actualizar producto existente")
          .RequireModule("catalog", canUpdate: true)
          .Produces<ProductResponseDto>(200)
          .Produces(404)
          .ProducesValidationProblem()
          .ProducesProblem(401)
          .ProducesProblem(403);

      // DELETE /api/admin/products/{id} - Eliminar producto (soft delete)
      group.MapDelete("/{id:guid}", DeleteProduct)
          .WithName("AdminDeleteProduct")
          .WithSummary("Eliminar producto (soft delete)")
          .WithDescription("Marca el producto como inactivo en lugar de eliminarlo permanentemente")
          .RequireModule("catalog", canDelete: true)
          .Produces(204)
          .Produces(404)
          .ProducesProblem(401)
          .ProducesProblem(403);

      // ==================== OPERACIONES ESPECIALES ====================

      // PATCH /api/admin/products/{id}/stock - Actualizar stock
      group.MapPatch("/{id:guid}/stock", UpdateStock)
          .WithName("AdminUpdateProductStock")
          .WithSummary("Actualizar stock del producto")
          .RequireModule("catalog", canUpdate: true)
          .Produces(204)
          .Produces(404)
          .ProducesProblem(401);

      // PATCH /api/admin/products/{id}/toggle-featured - Alternar destacado
      group.MapPatch("/{id:guid}/toggle-featured", ToggleFeatured)
          .WithName("AdminToggleProductFeatured")
          .WithSummary("Alternar estado de producto destacado")
          .RequireModule("catalog", canUpdate: true)
          .Produces(204)
          .Produces(404)
          .ProducesProblem(401);
    }

    // ==================== HANDLER FUNCTIONS ====================

    private static async Task<IResult> GetProducts(
        [FromServices] IProductService productService,
        [AsParameters] ProductFilterDto filter,
        CancellationToken ct)
    {
      try
      {
        var result = await productService.GetPagedAsync(filter, ct);
        return Results.Ok(result);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al obtener productos");
      }
    }

    private static async Task<IResult> GetProductById(
        Guid id,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        var product = await productService.GetByIdAsync(id, ct);

        if (product == null)
        {
          return Results.NotFound(new { message = $"Producto {id} no encontrado" });
        }

        return Results.Ok(product);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al obtener producto");
      }
    }

    private static async Task<IResult> GetProductBySlug(
        string slug,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        var product = await productService.GetBySlugAsync(slug, ct);

        if (product == null)
        {
          return Results.NotFound(new { message = $"Producto con slug '{slug}' no encontrado" });
        }

        return Results.Ok(product);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al obtener producto");
      }
    }

    private static async Task<IResult> CreateProduct(
        [FromBody] CreateProductDto dto,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        // Validaciones básicas
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
          return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        { "Name", new[] { "El nombre del producto es requerido" } }
                    });
        }

        if (dto.Price <= 0)
        {
          return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        { "Price", new[] { "El precio debe ser mayor a 0" } }
                    });
        }

        var product = await productService.CreateAsync(dto, ct);

        return Results.Created($"/api/products/{product.Id}", product);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al crear producto");
      }
    }

    private static async Task<IResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductDto dto,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        // Validaciones
        if (dto.Price.HasValue && dto.Price <= 0)
        {
          return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        { "Price", new[] { "El precio debe ser mayor a 0" } }
                    });
        }

        var product = await productService.UpdateAsync(id, dto, ct);

        return Results.Ok(product);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
      {
        return Results.NotFound(new { message = ex.Message });
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al actualizar producto");
      }
    }

    private static async Task<IResult> DeleteProduct(
        Guid id,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        var deleted = await productService.DeleteAsync(id, ct);

        if (!deleted)
        {
          return Results.NotFound(new { message = $"Producto {id} no encontrado" });
        }

        return Results.NoContent();
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al eliminar producto");
      }
    }

    private static async Task<IResult> UpdateStock(
        Guid id,
        [FromBody] UpdateStockRequest request,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        if (request.Quantity < 0)
        {
          return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        { "Quantity", new[] { "La cantidad no puede ser negativa" } }
                    });
        }

        var updated = await productService.UpdateStockAsync(id, request.Quantity, ct);

        if (!updated)
        {
          return Results.NotFound(new { message = $"Producto {id} no encontrado" });
        }

        return Results.NoContent();
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al actualizar stock");
      }
    }

    private static async Task<IResult> ToggleFeatured(
        Guid id,
        [FromServices] IProductService productService,
        CancellationToken ct)
    {
      try
      {
        var toggled = await productService.ToggleFeaturedAsync(id, ct);

        if (!toggled)
        {
          return Results.NotFound(new { message = $"Producto {id} no encontrado" });
        }

        return Results.NoContent();
      }
      catch (Exception ex)
      {
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Error al cambiar estado destacado");
      }
    }
  }

  // ==================== REQUEST DTOs ====================

  public record UpdateStockRequest(int Quantity);
}
