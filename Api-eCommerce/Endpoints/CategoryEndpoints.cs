using Api_eCommerce.Authorization;
using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api_eCommerce.Endpoints
{
  public static class CategoryEndpoints
  {
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
      var group = app.MapGroup("/api/categories")
          .WithTags("Categories");

      // ==================== LECTURA (público o con permiso CanView) ====================

      group.MapGet("/", GetAll)
          .WithName("GetCategories")
          .WithSummary("Obtiene todas las categorías con paginación y filtros")
          .WithDescription("Endpoint público para listar categorías. Soporta búsqueda, filtro por estado y paginación.")
          .AllowAnonymous() // Público para el frontend
          .Produces<CategoryListResponse>(StatusCodes.Status200OK);

      group.MapGet("/{id:guid}", GetById)
          .WithName("GetCategoryById")
          .WithSummary("Obtiene una categoría por ID")
          .AllowAnonymous()
          .Produces<CategoryResponse>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

      group.MapGet("/slug/{slug}", GetBySlug)
          .WithName("GetCategoryBySlugAdmin")
          .WithSummary("Obtiene una categoría por slug (URL amigable)")
          .AllowAnonymous()
          .Produces<CategoryResponse>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

      // ==================== CREACIÓN (requiere CanCreate en módulo 'categories') ====================

      group.MapPost("/", Create)
          .WithName("CreateCategory")
          .WithSummary("Crea una nueva categoría")
          .WithDescription("Requiere permiso CanCreate en el módulo 'categories'. Admin y Manager pueden crear.")
          .RequireAuthorization()
          .AddEndpointFilter<ModuleAuthorizationFilter>() // Valida permisos
          .WithMetadata(new RequireModuleAttribute("categories", "create"))
          .Produces<CategoryResponse>(StatusCodes.Status201Created)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
          .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

      // ==================== ACTUALIZACIÓN (requiere CanUpdate) ====================

      group.MapPut("/{id:guid}", Update)
          .WithName("UpdateCategory")
          .WithSummary("Actualiza una categoría existente")
          .WithDescription("Requiere permiso CanUpdate en el módulo 'categories'. Admin y Manager pueden actualizar.")
          .RequireAuthorization()
          .AddEndpointFilter<ModuleAuthorizationFilter>()
          .WithMetadata(new RequireModuleAttribute("categories", "update"))
          .Produces<CategoryResponse>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
          .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
          .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

      // ==================== ELIMINACIÓN (requiere CanDelete) ====================

      group.MapDelete("/{id:guid}", Delete)
          .WithName("DeleteCategory")
          .WithSummary("Elimina una categoría de forma física")
          .WithDescription("Requiere permiso CanDelete en el módulo 'categories'. Solo Admin puede eliminar. Desvincula automáticamente los productos asociados.")
          .RequireAuthorization()
          .AddEndpointFilter<ModuleAuthorizationFilter>()
          .WithMetadata(new RequireModuleAttribute("categories", "delete"))
          .Produces(StatusCodes.Status204NoContent)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
          .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
          .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

      return app;
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> GetAll(
        HttpContext context,
        [FromServices] ICategoryManagementService categoryService,
        [FromServices] ITenantResolver tenantResolver,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
      try
      {
        // Validar tenant
        var tenantContext = await tenantResolver.ResolveAsync(context);
        if (tenantContext == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Validar paginación
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await categoryService.GetAllAsync(page, pageSize, search, isActive);
        return Results.Ok(result);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    private static async Task<IResult> GetById(
        Guid id,
        HttpContext context,
        [FromServices] ICategoryManagementService categoryService,
        [FromServices] ITenantResolver tenantResolver)
    {
      try
      {
        var tenantContext = await tenantResolver.ResolveAsync(context);
        if (tenantContext == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var category = await categoryService.GetByIdAsync(id);
        return category != null
            ? Results.Ok(category)
            : Results.NotFound(new { message = "Categoría no encontrada" });
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    private static async Task<IResult> GetBySlug(
        string slug,
        HttpContext context,
        [FromServices] ICategoryManagementService categoryService,
        [FromServices] ITenantResolver tenantResolver)
    {
      try
      {
        var tenantContext = await tenantResolver.ResolveAsync(context);
        if (tenantContext == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var category = await categoryService.GetBySlugAsync(slug);
        return category != null
            ? Results.Ok(category)
            : Results.NotFound(new { message = $"Categoría con slug '{slug}' no encontrada" });
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    private static async Task<IResult> Create(
        HttpContext context,
        [FromBody] CreateCategoryRequest request,
        [FromServices] ICategoryManagementService categoryService,
        [FromServices] ITenantResolver tenantResolver)
    {
      try
      {
        var tenantContext = await tenantResolver.ResolveAsync(context);
        if (tenantContext == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var category = await categoryService.CreateAsync(request);
        return Results.Created($"/api/categories/{category.Id}", category);
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    private static async Task<IResult> Update(
        Guid id,
        HttpContext context,
        [FromBody] UpdateCategoryRequest request,
        [FromServices] ICategoryManagementService categoryService,
        [FromServices] ITenantResolver tenantResolver)
    {
      try
      {
        var tenantContext = await tenantResolver.ResolveAsync(context);
        if (tenantContext == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Validar que el ID del path coincida con el del body
        if (id != request.Id)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "ID Mismatch",
              detail: "El ID de la URL no coincide con el ID del cuerpo de la petición"
          );
        }

        var category = await categoryService.UpdateAsync(request);
        return Results.Ok(category);
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    private static async Task<IResult> Delete(
        Guid id,
        HttpContext context,
        [FromServices] ICategoryManagementService categoryService,
        [FromServices] ITenantResolver tenantResolver)
    {
      try
      {
        var tenantContext = await tenantResolver.ResolveAsync(context);
        if (tenantContext == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        await categoryService.DeleteAsync(id);
        return Results.NoContent();
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }
  }
}
