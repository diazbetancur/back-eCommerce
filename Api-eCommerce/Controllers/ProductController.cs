using Api_eCommerce.Authorization;
using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using CC.Domain.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para productos - Admin (CRUD completo con permisos)
  /// </summary>
  [ApiController]
  [Route("api/admin/products")]
  [Tags("Products - Admin")]
  [Authorize]
  public class ProductAdminController : ControllerBase
  {
    private readonly IProductService _productService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<ProductAdminController> _logger;

    public ProductAdminController(
        IProductService productService,
        ITenantResolver tenantResolver,
        ILogger<ProductAdminController> logger)
    {
      _productService = productService;
      _tenantResolver = tenantResolver;
      _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los productos con paginación y filtros
    /// </summary>
    /// <param name="filter">Filtros de búsqueda y paginación</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Lista paginada de productos</returns>
    [HttpGet]
    [RequireModule("catalog", "view")]
    [ProducesResponseType<PagedResult<ProductResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductFilterDto filter,
        CancellationToken ct)
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

        var result = await _productService.GetPagedAsync(filter, ct);
        return Ok(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener productos paginados");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener productos",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por ID
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("{id:guid}")]
    [RequireModule("catalog", "view")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProductById(
        Guid id,
        CancellationToken ct)
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

        var product = await _productService.GetByIdAsync(id, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por slug (URL amigable)
    /// </summary>
    /// <param name="slug">Slug del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("slug/{slug}")]
    [RequireModule("catalog", "view")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProductBySlug(
        string slug,
        CancellationToken ct)
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

        var product = await _productService.GetBySlugAsync(slug, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con slug '{slug}' no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto por slug {Slug}", slug);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Crea un nuevo producto
    /// </summary>
    /// <param name="dto">Datos del producto a crear</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto creado</returns>
    [HttpPost]
    [RequireModule("catalog", "create")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductDto dto,
        CancellationToken ct)
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

        // Validaciones básicas
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Name", new[] { "El nombre del producto es requerido" } }
                    }
          });
        }

        if (dto.Price <= 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Price", new[] { "El precio debe ser mayor a 0" } }
                    }
          });
        }

        var product = await _productService.CreateAsync(dto, ct);

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.Id },
            product
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al crear producto");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al crear producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualiza un producto existente
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="dto">Datos a actualizar</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto actualizado</returns>
    [HttpPut("{id:guid}")]
    [RequireModule("catalog", "update")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductDto dto,
        CancellationToken ct)
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

        // Validaciones
        if (dto.Price.HasValue && dto.Price <= 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Price", new[] { "El precio debe ser mayor a 0" } }
                    }
          });
        }

        var product = await _productService.UpdateAsync(id, dto, ct);
        return Ok(product);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Product Not Found",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al actualizar producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al actualizar producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Elimina un producto (soft delete)
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Sin contenido si fue exitoso</returns>
    [HttpDelete("{id:guid}")]
    [RequireModule("catalog", "delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken ct)
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

        var deleted = await _productService.DeleteAsync(id, ct);

        if (!deleted)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al eliminar producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al eliminar producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualiza el stock de un producto
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="request">Nueva cantidad de stock</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Sin contenido si fue exitoso</returns>
    [HttpPatch("{id:guid}/stock")]
    [RequireModule("catalog", "update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateStock(
        Guid id,
        [FromBody] UpdateStockRequest request,
        CancellationToken ct)
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

        if (request.Quantity < 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Quantity", new[] { "La cantidad no puede ser negativa" } }
                    }
          });
        }

        var updated = await _productService.UpdateStockAsync(id, request.Quantity, ct);

        if (!updated)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al actualizar stock del producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al actualizar stock",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Alterna el estado de producto destacado
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Sin contenido si fue exitoso</returns>
    [HttpPatch("{id:guid}/toggle-featured")]
    [RequireModule("catalog", "update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ToggleFeatured(
        Guid id,
        CancellationToken ct)
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

        var toggled = await _productService.ToggleFeaturedAsync(id, ct);

        if (!toggled)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al cambiar estado destacado del producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al cambiar estado destacado",
            detail: ex.Message
        );
      }
    }
  }

  // ==================== CONTROLLER PÚBLICO ====================

  /// <summary>
  /// Controlador público de productos para storefront (sin autenticación)
  /// </summary>
  [ApiController]
  [Route("api/products")]
  [Tags("Products - Public")]
  public class ProductController : ControllerBase
  {
    private readonly IProductService _productService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<ProductController> _logger;

    public ProductController(
        IProductService productService,
        ITenantResolver tenantResolver,
        ILogger<ProductController> logger)
    {
      _productService = productService;
      _tenantResolver = tenantResolver;
      _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los productos (público - para storefront)
    /// </summary>
    /// <param name="filter">Filtros de búsqueda y paginación</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Lista paginada de productos</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ProductResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPublicProducts(
        [FromQuery] ProductFilterDto filter,
        CancellationToken ct)
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

        var result = await _productService.GetPagedAsync(filter, ct);
        return Ok(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener productos públicos");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener productos",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por ID (público)
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPublicProductById(
        Guid id,
        CancellationToken ct)
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

        var product = await _productService.GetByIdAsync(id, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto público {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por slug (público - para URLs amigables)
    /// </summary>
    /// <param name="slug">Slug del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPublicProductBySlug(
        string slug,
        CancellationToken ct)
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

        var product = await _productService.GetBySlugAsync(slug, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con slug '{slug}' no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto público por slug {Slug}", slug);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }
  }
}
