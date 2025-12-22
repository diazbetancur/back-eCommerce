using CC.Aplication.Loyalty;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para gestión de programa de lealtad
  /// </summary>
  [ApiController]
  [Route("me/loyalty")]
  [Authorize]
  [Tags("User Loyalty")]
  public class LoyaltyController : ControllerBase
  {
    private readonly ILoyaltyService _loyaltyService;
    private readonly ITenantResolver _tenantResolver;

    public LoyaltyController(ILoyaltyService loyaltyService, ITenantResolver tenantResolver)
    {
      _loyaltyService = loyaltyService;
      _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Get user's loyalty account
    /// </summary>
    [HttpGet]
    [Api_eCommerce.Authorization.RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(Api_eCommerce.Authorization.ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyAccountSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLoyalty()
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
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Obtener cuenta de loyalty
        var loyalty = await _loyaltyService.GetUserLoyaltyAsync(userId.Value);
        return Ok(loyalty);
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
            detail: "An error occurred while retrieving loyalty information"
        );
      }
    }

    /// <summary>
    /// Get user's loyalty transactions
    /// </summary>
    [HttpGet("transactions")]
    [Api_eCommerce.Authorization.RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(Api_eCommerce.Authorization.ModuleAuthorizationActionFilter))]
    [ProducesResponseType<PagedLoyaltyTransactionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
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
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Crear query
        var query = new GetLoyaltyTransactionsQuery(
            Page: page,
            PageSize: pageSize,
            Type: type,
            FromDate: fromDate,
            ToDate: toDate
        );

        // Obtener transacciones
        var transactions = await _loyaltyService.GetUserTransactionsAsync(userId.Value, query);
        return Ok(transactions);
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
            detail: "An error occurred while retrieving transactions"
        );
      }
    }

    private Guid? GetUserIdFromJwt()
    {
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
          ?? User.FindFirst("sub");

      if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
      {
        return userId;
      }

      return null;
    }
  }
}
