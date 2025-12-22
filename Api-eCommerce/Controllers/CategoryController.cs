using Api_eCommerce.Authorization;
using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para gestión de categorías
  /// </summary>
  [ApiController]
  [Route("api/categories")]
  [Tags("Categories")]
  public class CategoryController : ControllerBase
  {
    private readonly ICategoryManagementService _categoryService;
    private readonly ITenantResolver _tenantResolver;

    public CategoryController(ICategoryManagementService categoryService, ITenantResolver tenantResolver)
    {
      _categoryService = categoryService;
      _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Obtiene todas las categorías con paginación y filtros
    /// </summary>
    /// <param name="page">Número de página (default: 1)</param>
    /// <param name="pageSize">Tamaño de página (default: 20, máx: 100)</param>
    /// <param name="search">Búsqueda por nombre o slug (opcional)</param>
    /// <param name="isActive">Filtrar por estado activo (opcional)</param>
    /// <returns>Lista paginada de categorías</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<CategoryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Validar paginación
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _categoryService.GetAllAsync(page, pageSize, search, isActive);
        return Ok(result);
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene una categoría por ID
    /// </summary>
    /// <param name="id">ID de la categoría</param>
    /// <returns>Categoría encontrada</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(Guid id)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var category = await _categoryService.GetByIdAsync(id);
        return category != null
            ? Ok(category)
            : NotFound(new { message = "Categoría no encontrada" });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene una categoría por slug (URL amigable)
    /// </summary>
    /// <param name="slug">Slug de la categoría</param>
    /// <returns>Categoría encontrada</returns>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var category = await _categoryService.GetBySlugAsync(slug);
        return category != null
            ? Ok(category)
            : NotFound(new { message = $"Categoría con slug '{slug}' no encontrada" });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Crea una nueva categoría
    /// </summary>
    /// <param name="request">Datos de la categoría</param>
    /// <returns>Categoría creada</returns>
    /// <remarks>
    /// Requiere permiso CanCreate en el módulo 'catalog'. Admin y Manager pueden crear.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [RequireModule("catalog", "create")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var category = await _categoryService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualiza una categoría existente
    /// </summary>
    /// <param name="id">ID de la categoría</param>
    /// <param name="request">Datos actualizados</param>
    /// <returns>Categoría actualizada</returns>
    /// <remarks>
    /// Requiere permiso CanUpdate en el módulo 'catalog'. Admin y Manager pueden actualizar.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize]
    [RequireModule("catalog", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Validar que el ID del path coincida con el del body
        if (id != request.Id)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "ID Mismatch",
              detail: "El ID de la URL no coincide con el ID del cuerpo de la petición"
          );
        }

        var category = await _categoryService.UpdateAsync(request);
        return Ok(category);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Elimina una categoría de forma física
    /// </summary>
    /// <param name="id">ID de la categoría</param>
    /// <returns>NoContent</returns>
    /// <remarks>
    /// Requiere permiso CanDelete en el módulo 'catalog'. Solo Admin puede eliminar.
    /// Desvincula automáticamente los productos asociados.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [RequireModule("catalog", "delete")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid id)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        await _categoryService.DeleteAsync(id);
        return NoContent();
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: ex.Message
        );
      }
    }
  }
}
