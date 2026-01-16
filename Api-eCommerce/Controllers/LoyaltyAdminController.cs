using Api_eCommerce.Authorization;
using CC.Aplication.Loyalty;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador administrativo para gestión del programa de lealtad
  /// </summary>
  [ApiController]
  [Route("admin/loyalty")]
  [Authorize]
  [Tags("Loyalty Admin")]
  public class LoyaltyAdminController : ControllerBase
  {
    private readonly ILoyaltyRewardsService _rewardsService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly ITenantResolver _tenantResolver;

    public LoyaltyAdminController(
        ILoyaltyRewardsService rewardsService,
        ILoyaltyService loyaltyService,
        ITenantResolver tenantResolver)
    {
      _rewardsService = rewardsService;
      _loyaltyService = loyaltyService;
      _tenantResolver = tenantResolver;
    }

    // ==================== REWARDS MANAGEMENT ====================

    /// <summary>
    /// Crear un nuevo premio de lealtad
    /// </summary>
    [HttpPost("rewards")]
    [RequireModule("loyalty", "create")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateReward([FromBody] CreateLoyaltyRewardRequest request)
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

        var reward = await _rewardsService.CreateRewardAsync(request);
        return CreatedAtAction(nameof(GetRewardById), new { id = reward.Id }, reward);
      }
      catch (ArgumentException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while creating the reward"
        );
      }
    }

    /// <summary>
    /// Obtener lista de premios (con filtros y paginación)
    /// </summary>
    [HttpGet("rewards")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<PagedLoyaltyRewardsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRewards(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? rewardType = null)
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

        var query = new GetLoyaltyRewardsQuery(page, pageSize, isActive, rewardType);
        var rewards = await _rewardsService.GetRewardsAsync(query);
        return Ok(rewards);
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving rewards"
        );
      }
    }

    /// <summary>
    /// Obtener un premio por ID
    /// </summary>
    [HttpGet("rewards/{id:guid}")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRewardById(Guid id)
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

        var reward = await _rewardsService.GetRewardByIdAsync(id);
        if (reward == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = $"Reward {id} not found"
          });
        }

        return Ok(reward);
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving the reward"
        );
      }
    }

    /// <summary>
    /// Actualizar un premio existente
    /// </summary>
    [HttpPut("rewards/{id:guid}")]
    [RequireModule("loyalty", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateReward(Guid id, [FromBody] UpdateLoyaltyRewardRequest request)
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

        var reward = await _rewardsService.UpdateRewardAsync(id, request);
        return Ok(reward);
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Not Found",
          Detail = ex.Message
        });
      }
      catch (ArgumentException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while updating the reward"
        );
      }
    }

    /// <summary>
    /// Eliminar un premio (soft delete si tiene canjes)
    /// </summary>
    [HttpDelete("rewards/{id:guid}")]
    [RequireModule("loyalty", "delete")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteReward(Guid id)
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

        await _rewardsService.DeleteRewardAsync(id);
        return NoContent();
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Not Found",
          Detail = ex.Message
        });
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while deleting the reward"
        );
      }
    }

    // ==================== REDEMPTIONS MANAGEMENT ====================

    /// <summary>
    /// Ver todos los canjes realizados (admin)
    /// </summary>
    [HttpGet("redemptions")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<PagedLoyaltyRedemptionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRedemptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
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

        var query = new GetLoyaltyRedemptionsQuery(page, pageSize, status, userId, fromDate, toDate);
        var redemptions = await _rewardsService.GetRedemptionsAsync(query);
        return Ok(redemptions);
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving redemptions"
        );
      }
    }

    /// <summary>
    /// Actualizar estado de un canje (aprobar, entregar, cancelar)
    /// </summary>
    [HttpPatch("redemptions/{id:guid}/status")]
    [RequireModule("loyalty", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyRedemptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRedemptionStatus(
        Guid id,
        [FromBody] UpdateRedemptionStatusRequest request)
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

        var redemption = await _rewardsService.UpdateRedemptionStatusAsync(id, request);
        return Ok(redemption);
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Not Found",
          Detail = ex.Message
        });
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while updating redemption status"
        );
      }
    }

    // ==================== LOYALTY CONFIGURATION ====================

    /// <summary>
    /// Obtener configuración del programa de lealtad
    /// </summary>
    [HttpGet("config")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetConfiguration()
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

        var config = await _loyaltyService.GetLoyaltyConfigurationAsync();
        return Ok(config);
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving loyalty configuration"
        );
      }
    }

    /// <summary>
    /// Actualizar configuración del programa de lealtad
    /// </summary>
    [HttpPut("config")]
    [RequireModule("loyalty", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateConfiguration([FromBody] UpdateLoyaltyConfigRequest request)
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

        var config = await _loyaltyService.UpdateLoyaltyConfigurationAsync(request);
        return Ok(config);
      }
      catch (ArgumentException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while updating loyalty configuration"
        );
      }
    }

    // ==================== MANUAL POINTS ADJUSTMENT ====================

    /// <summary>
    /// Ajustar puntos manualmente (compra física, regalo, corrección)
    /// </summary>
    [HttpPost("points/adjust")]
    [RequireModule("loyalty", "create")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<AdjustPointsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AdjustPoints([FromBody] AdjustPointsRequest request)
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

        var result = await _loyaltyService.AdjustPointsManuallyAsync(request);
        return Ok(result);
      }
      catch (ArgumentException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
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
            detail: "An error occurred while adjusting points"
        );
      }
    }
  }
}
