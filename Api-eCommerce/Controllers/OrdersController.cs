using CC.Aplication.Orders;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para gestión de órdenes de usuario
  /// </summary>
  [ApiController]
  [Route("me/orders")]
  [Authorize]
  [Tags("User Orders")]
  public class OrdersController : ControllerBase
  {
    private readonly IOrderService _orderService;
    private readonly ITenantResolver _tenantResolver;

    public OrdersController(IOrderService orderService, ITenantResolver tenantResolver)
    {
      _orderService = orderService;
      _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Get user's order history
    /// </summary>
    [HttpGet]
    [Api_eCommerce.Authorization.RequireModule("orders", "view")]
    [ServiceFilter(typeof(Api_eCommerce.Authorization.ModuleAuthorizationActionFilter))]
    [ProducesResponseType<PagedOrdersResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Obtener user ID del token JWT
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Crear query
        var query = new GetOrdersQuery(
            Page: page,
            PageSize: pageSize,
            Status: status,
            FromDate: fromDate,
            ToDate: toDate
        );

        // Obtener órdenes
        var orders = await _orderService.GetUserOrdersAsync(userId, query);
        return Ok(orders);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Operation Failed",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving orders"
        );
      }
    }

    /// <summary>
    /// Get order details
    /// </summary>
    [HttpGet("{orderId:guid}")]
    [Api_eCommerce.Authorization.RequireModule("orders", "view")]
    [ServiceFilter(typeof(Api_eCommerce.Authorization.ModuleAuthorizationActionFilter))]
    [ProducesResponseType<OrderDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderDetail(Guid orderId)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Obtener user ID del token JWT
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Obtener detalle de la orden
        var order = await _orderService.GetOrderDetailAsync(userId, orderId);

        if (order == null)
        {
          return Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Order Not Found",
              detail: $"Order {orderId} not found or does not belong to the user"
          );
        }

        return Ok(order);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Operation Failed",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving order details"
        );
      }
    }
  }
}
