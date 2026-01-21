using Api_eCommerce.Authorization;
using CC.Aplication.Stores;
using CC.Domain.Dto;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador administrativo para gestión de tiendas (multi-location inventory)
  /// </summary>
  [ApiController]
  [Route("api/admin/stores")]
  [Authorize]
  [Tags("Stores Admin")]
  public class StoresAdminController : ControllerBase
  {
    private readonly IStoreService _storeService;
    private readonly IStockService _stockService;
    private readonly ITenantResolver _tenantResolver;

    public StoresAdminController(
        IStoreService storeService,
        IStockService stockService,
        ITenantResolver tenantResolver)
    {
      _storeService = storeService;
      _stockService = stockService;
      _tenantResolver = tenantResolver;
    }

    // ==================== STORES MANAGEMENT ====================

    /// <summary>
    /// Obtener todas las tiendas del tenant
    /// </summary>
    [HttpGet]
    [RequireModule("inventory", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<List<StoreDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStores([FromQuery] bool includeInactive = false)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var stores = await _storeService.GetAllStoresAsync(includeInactive);
        return Ok(stores);
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error retrieving stores",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtener una tienda por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequireModule("inventory", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<StoreDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoreById(Guid id)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var store = await _storeService.GetStoreByIdAsync(id);
        if (store == null)
        {
          return NotFound($"Store with ID '{id}' not found");
        }

        return Ok(store);
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error retrieving store",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Crear una nueva tienda
    /// </summary>
    [HttpPost]
    [RequireModule("inventory", "create")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<StoreDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var store = await _storeService.CreateStoreAsync(request);
        return CreatedAtAction(nameof(GetStoreById), new { id = store.Id }, store);
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid operation",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error creating store",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualizar una tienda existente
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequireModule("inventory", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<StoreDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStore(Guid id, [FromBody] UpdateStoreRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var store = await _storeService.UpdateStoreAsync(id, request);
        return Ok(store);
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid operation",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error updating store",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Eliminar una tienda
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequireModule("inventory", "delete")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStore(Guid id)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        await _storeService.DeleteStoreAsync(id);
        return NoContent();
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid operation",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error deleting store",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Establecer una tienda como predeterminada
    /// </summary>
    [HttpPost("{id:guid}/set-default")]
    [RequireModule("inventory", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<StoreDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultStore(Guid id)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var store = await _storeService.SetDefaultStoreAsync(id);
        return Ok(store);
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid operation",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error setting default store",
            detail: ex.Message
        );
      }
    }

    // ==================== STOCK MANAGEMENT ====================

    /// <summary>
    /// Obtener el stock de un producto en todas las tiendas
    /// </summary>
    [HttpGet("products/{productId:guid}/stock")]
    [RequireModule("inventory", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<List<ProductStoreStockDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductStock(Guid productId)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var stock = await _stockService.GetProductStockByStoresAsync(productId);
        return Ok(stock);
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error retrieving product stock",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualizar el stock de un producto en una tienda específica
    /// </summary>
    [HttpPut("products/{productId:guid}/stock")]
    [RequireModule("inventory", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<ProductStoreStockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProductStock(Guid productId, [FromBody] UpdateProductStoreStockRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var stock = await _stockService.UpdateProductStoreStockAsync(productId, request);
        return Ok(stock);
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid operation",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error updating product stock",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Verificar disponibilidad de stock
    /// </summary>
    [HttpPost("products/{productId:guid}/check-stock")]
    [RequireModule("inventory", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<StockAvailabilityResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckStock(Guid productId, [FromBody] StockAvailabilityRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var result = await _stockService.CheckStockAsync(productId, request.Quantity, request.StoreId);
        return Ok(result);
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error checking stock",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Migrar stock legacy (Product.Stock) a una tienda específica
    /// </summary>
    [HttpPost("migrate-legacy-stock")]
    [RequireModule("inventory", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<MigrateLegacyStockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MigrateLegacyStock([FromBody] MigrateStockToStoresRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var migratedCount = await _stockService.MigrateAllLegacyStockToStoreAsync(request.DefaultStoreId);

        return Ok(new MigrateLegacyStockResponse
        {
          MigratedProductsCount = migratedCount,
          TargetStoreId = request.DefaultStoreId,
          Message = $"Successfully migrated stock for {migratedCount} products"
        });
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid operation",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error migrating legacy stock",
            detail: ex.Message
        );
      }
    }

    // ==================== STORE STOCK MANAGEMENT ====================

    /// <summary>
    /// Obtener el stock de todos los productos en una tienda específica
    /// </summary>
    /// <param name="storeId">ID de la tienda</param>
    /// <response code="200">Lista de productos con su stock en la tienda</response>
    /// <response code="403">No tiene permisos para ver inventario</response>
    /// <response code="404">Tienda no encontrada</response>
    [HttpGet("{storeId:guid}/stock")]
    [RequireModule("inventory", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<List<StoreProductStockDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoreStock(Guid storeId)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var stock = await _stockService.GetStoreStockAsync(storeId);
        return Ok(stock);
      }
      catch (InvalidOperationException ex)
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Store not found",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error retrieving store stock",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualizar el stock de un producto en una tienda específica
    /// </summary>
    /// <param name="storeId">ID de la tienda</param>
    /// <param name="productId">ID del producto</param>
    /// <param name="request">Nuevo valor de stock</param>
    /// <remarks>
    /// El stock debe ser mayor o igual a 0. La operación es idempotente.
    /// El ReservedStock no se modifica, solo el Stock total.
    /// </remarks>
    /// <response code="200">Stock actualizado exitosamente</response>
    /// <response code="400">Stock inválido (negativo)</response>
    /// <response code="403">No tiene permisos para actualizar inventario</response>
    /// <response code="404">Tienda o producto no encontrado</response>
    [HttpPut("{storeId:guid}/stock/{productId:guid}")]
    [RequireModule("inventory", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<StoreProductStockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStoreProductStock(
        Guid storeId,
        Guid productId,
        [FromBody] UpdateStoreStockRequest request)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var updatedStock = await _stockService.UpdateStoreProductStockAsync(storeId, productId, request.Stock);
        return Ok(updatedStock);
      }
      catch (ArgumentException ex)
      {
        return BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid stock value",
          Detail = ex.Message
        });
      }
      catch (InvalidOperationException ex)
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Store or Product not found",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error updating stock",
            detail: ex.Message
        );
      }
    }
  }
}
