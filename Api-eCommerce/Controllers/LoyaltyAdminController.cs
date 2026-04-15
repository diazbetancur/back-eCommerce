using Api_eCommerce.Authorization;
using CC.Aplication.Loyalty;
using CC.Domain.Assets;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador administrativo para gestión del programa de lealtad
  /// </summary>
  [ApiController]
  [Route("api/admin/loyalty")]
  [Authorize]
  [Tags("Loyalty Admin")]
  public class LoyaltyAdminController : ControllerBase
  {
    private readonly ILoyaltyRewardsService _rewardsService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IAssetService _assetService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<LoyaltyAdminController> _logger;

    public LoyaltyAdminController(
        ILoyaltyRewardsService rewardsService,
        ILoyaltyService loyaltyService,
        IAssetService assetService,
        ITenantResolver tenantResolver,
        ILogger<LoyaltyAdminController> logger)
    {
      _rewardsService = rewardsService;
      _loyaltyService = loyaltyService;
      _assetService = assetService;
      _tenantResolver = tenantResolver;
      _logger = logger;
    }

    // ==================== REWARDS MANAGEMENT ====================

    /// <summary>
    /// Crear un nuevo premio de lealtad
    /// </summary>
    [HttpPost("rewards")]
    [RequireModule("loyalty", "create")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [Consumes("application/json")]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateReward([FromBody] CreateLoyaltyRewardRequest request, CancellationToken ct)
    {
      return await CreateRewardInternalAsync(request, image: null, ct);
    }

    /// <summary>
    /// Crear un nuevo premio de lealtad con carga de imagen
    /// </summary>
    [HttpPost("rewards")]
    [RequireModule("loyalty", "create")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRewardForm([FromForm] CreateLoyaltyRewardFormRequest request, CancellationToken ct)
    {
      if (request.Image is { Length: <= 0 })
      {
        return ValidationProblem(new ValidationProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Errors = new Dictionary<string, string[]>
          {
            ["image"] = new[] { "La imagen no puede estar vacía." }
          }
        });
      }

      var createRequest = MapCreateRequest(request);
      return await CreateRewardInternalAsync(createRequest, request.Image, ct);
    }

    private async Task<IActionResult> CreateRewardInternalAsync(
      CreateLoyaltyRewardRequest request,
      IFormFile? image,
      CancellationToken ct)
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

        var reward = await _rewardsService.CreateRewardAsync(request, ct);

        if (image is { Length: > 0 })
        {
          Guid? uploadedAssetId = null;

          try
          {
            var uploadedAsset = await UploadRewardImageAsync(reward.Id, tenantContext.TenantId, image, ct);
            uploadedAssetId = uploadedAsset.Id;

            var refreshedReward = await _rewardsService.GetRewardByIdAsync(reward.Id, ct);
            if (refreshedReward != null)
            {
              reward = refreshedReward;
            }
          }
          catch (Exception ex)
          {
            await DeleteUploadedAssetBestEffortAsync(tenantContext.TenantId, uploadedAssetId, ct);
            await DeleteRewardBestEffortAsync(reward.Id, ct);

            _logger.LogWarning(
                ex,
                "Error al cargar la imagen para la recompensa {RewardId}. Se revirtió la creación.",
                reward.Id);

            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Error de validación",
                detail: "No fue posible cargar la imagen de la recompensa. La creación se canceló para evitar datos incompletos.");
          }
        }

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
      [FromQuery] string? rewardType = null,
      [FromQuery] string? search = null,
      [FromQuery] DateTime? availableFrom = null,
      [FromQuery] DateTime? availableUntil = null,
      [FromQuery] DateTime? createdFrom = null,
      [FromQuery] DateTime? createdTo = null,
      [FromQuery] bool? isCurrentlyAvailable = null)
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

        var query = new GetLoyaltyRewardsQuery(
          Page: page,
          PageSize: pageSize,
          IsActive: isActive,
          RewardType: rewardType,
          Search: search,
          AvailableFrom: availableFrom,
          AvailableUntil: availableUntil,
          CreatedFrom: createdFrom,
          CreatedTo: createdTo,
          IsCurrentlyAvailable: isCurrentlyAvailable);
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
    [Consumes("application/json")]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateReward(Guid id, [FromBody] UpdateLoyaltyRewardRequest request, CancellationToken ct)
    {
      return await UpdateRewardInternalAsync(id, request, image: null, ct);
    }

    /// <summary>
    /// Actualizar un premio existente con carga de imagen
    /// </summary>
    [HttpPut("rewards/{id:guid}")]
    [RequireModule("loyalty", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<LoyaltyRewardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRewardForm(Guid id, [FromForm] UpdateLoyaltyRewardFormRequest request, CancellationToken ct)
    {
      if (request.Image is { Length: <= 0 })
      {
        return ValidationProblem(new ValidationProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Errors = new Dictionary<string, string[]>
          {
            ["image"] = new[] { "La imagen no puede estar vacía." }
          }
        });
      }

      var updateRequest = MapUpdateRequest(request);

      return await UpdateRewardInternalAsync(id, updateRequest, request.Image, ct);
    }

    private async Task<IActionResult> UpdateRewardInternalAsync(
      Guid id,
      UpdateLoyaltyRewardRequest request,
      IFormFile? image,
      CancellationToken ct)
    {
      Guid? uploadedAssetId = null;
      Guid tenantId = Guid.Empty;

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

        tenantId = tenantContext.TenantId;

        if (image is { Length: > 0 })
        {
          var uploadedAsset = await UploadRewardImageAsync(id, tenantId, image, ct);
          uploadedAssetId = uploadedAsset.Id;
        }

        var reward = await _rewardsService.UpdateRewardAsync(id, request, ct);
        return Ok(reward);
      }
      catch (KeyNotFoundException ex)
      {
        await DeleteUploadedAssetBestEffortAsync(tenantId, uploadedAssetId, ct);

        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Not Found",
          Detail = ex.Message
        });
      }
      catch (ArgumentException ex)
      {
        await DeleteUploadedAssetBestEffortAsync(tenantId, uploadedAssetId, ct);

        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (InvalidOperationException ex)
      {
        await DeleteUploadedAssetBestEffortAsync(tenantId, uploadedAssetId, ct);

        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation Error",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        await DeleteUploadedAssetBestEffortAsync(tenantId, uploadedAssetId, ct);

        _logger.LogError(ex, "Error al actualizar la recompensa {RewardId}", id);

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
      [FromQuery] string? userEmail = null,
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

        var query = new GetLoyaltyRedemptionsQuery(
          Page: page,
          PageSize: pageSize,
          Status: status,
          UserEmail: userEmail,
          FromDate: fromDate,
          ToDate: toDate);
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
    /// Obtener resumen de métricas para dashboard administrativo de loyalty
    /// </summary>
    [HttpGet("dashboard/summary")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyAdminDashboardSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboardSummary()
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

        var summary = await _loyaltyService.GetAdminDashboardSummaryAsync();
        return Ok(summary);
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving loyalty dashboard summary"
        );
      }
    }

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

    /// <summary>
    /// Obtener configuración de uso de puntos como dinero.
    /// </summary>
    [HttpGet("points-payment-config")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyPointsPaymentConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPointsPaymentConfiguration()
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

        var config = await _loyaltyService.GetLoyaltyPointsPaymentConfigAsync();
        return Ok(config);
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving loyalty points payment configuration"
        );
      }
    }

    /// <summary>
    /// Actualizar configuración de uso de puntos como dinero.
    /// </summary>
    [HttpPut("points-payment-config")]
    [RequireModule("loyalty", "update")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<LoyaltyPointsPaymentConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePointsPaymentConfiguration([FromBody] UpdateLoyaltyPointsPaymentConfigRequest request)
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

        var config = await _loyaltyService.UpdateLoyaltyPointsPaymentConfigAsync(request);
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
            detail: "An error occurred while updating loyalty points payment configuration"
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

        var adminUserId = GetUserIdFromClaims(User);
        if (!adminUserId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Unauthorized",
              detail: "User ID not found in token"
          );
        }

        var result = await _loyaltyService.AdjustPointsManuallyAsync(request, adminUserId.Value);
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
      catch (PostgresException ex) when (ex.SqlState == "42703")
      {
        _logger.LogError(ex, "Schema mismatch while adjusting manual points");
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Database Schema Outdated",
            detail: "Tenant DB schema is outdated for manual adjustments. Please apply pending tenant migrations."
        );
      }
      catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "42703")
      {
        _logger.LogError(ex, "Schema mismatch while adjusting manual points (wrapped DbUpdateException)");
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Database Schema Outdated",
            detail: "Tenant DB schema is outdated for manual adjustments. Please apply pending tenant migrations."
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error while adjusting points");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while adjusting points"
        );
      }
    }

    /// <summary>
    /// Listar historial de ajustes manuales de puntos
    /// </summary>
    [HttpGet("points/adjustments")]
    [RequireModule("loyalty", "view")]
    [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
    [ProducesResponseType<PagedManualPointAdjustmentsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetManualPointAdjustments(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20,
      [FromQuery] Guid? userId = null,
      [FromQuery] Guid? adjustedByUserId = null,
      [FromQuery] string? ticketNumber = null,
      [FromQuery] DateTime? fromDate = null,
      [FromQuery] DateTime? toDate = null,
      [FromQuery] string? search = null)
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

        var query = new GetManualPointAdjustmentsQuery(
          Page: page,
          PageSize: pageSize,
          UserId: userId,
          AdjustedByUserId: adjustedByUserId,
          TicketNumber: ticketNumber,
          FromDate: fromDate,
          ToDate: toDate,
          Search: search);

        var result = await _loyaltyService.GetManualPointAdjustmentsAsync(query);
        return Ok(result);
      }
      catch (PostgresException ex) when (ex.SqlState == "42703")
      {
        _logger.LogError(ex, "Schema mismatch while retrieving manual point adjustments");
        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Database Schema Outdated",
            detail: "Tenant DB schema is outdated for manual adjustments. Please apply pending tenant migrations."
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unexpected error while retrieving manual point adjustments");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving manual point adjustments"
        );
      }
    }

    private static Guid? GetUserIdFromClaims(ClaimsPrincipal user)
    {
      var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
          ?? user.FindFirst("sub");

      if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
      {
        return userId;
      }

      return null;
    }

    private static CreateLoyaltyRewardRequest MapCreateRequest(CreateLoyaltyRewardFormRequest request)
    {
      return new CreateLoyaltyRewardRequest
      {
        Name = request.Name,
        Description = request.Description,
        PointsCost = request.PointsCost,
        RewardType = request.RewardType,
        ProductIds = request.ProductIds,
        AppliesToAllEligibleProducts = request.AppliesToAllEligibleProducts,
        SingleProductSelectionRule = request.SingleProductSelectionRule,
        DiscountValue = request.DiscountValue,
        IsActive = request.IsActive,
        Stock = request.Stock,
        ValidityDays = request.ValidityDays,
        CouponQuantity = request.CouponQuantity,
        AvailableFrom = request.AvailableFrom,
        AvailableUntil = request.AvailableUntil,
        DisplayOrder = request.DisplayOrder
      };
    }

    private static UpdateLoyaltyRewardRequest MapUpdateRequest(UpdateLoyaltyRewardFormRequest request)
    {
      return new UpdateLoyaltyRewardRequest
      {
        Name = request.Name,
        Description = request.Description,
        PointsCost = request.PointsCost,
        RewardType = request.RewardType,
        ProductIds = request.ProductIds,
        AppliesToAllEligibleProducts = request.AppliesToAllEligibleProducts,
        SingleProductSelectionRule = request.SingleProductSelectionRule,
        DiscountValue = request.DiscountValue,
        IsActive = request.IsActive,
        Stock = request.Stock,
        ValidityDays = request.ValidityDays,
        CouponQuantity = request.CouponQuantity,
        AvailableFrom = request.AvailableFrom,
        AvailableUntil = request.AvailableUntil,
        DisplayOrder = request.DisplayOrder
      };
    }

    private async Task<TenantAssetDto> UploadRewardImageAsync(
      Guid rewardId,
      Guid tenantId,
      IFormFile image,
      CancellationToken ct)
    {
      var userId = User.FindFirst("sub")?.Value
          ?? User.FindFirst("id")?.Value
          ?? User.FindFirst("email")?.Value
          ?? "system";

      await using var stream = image.OpenReadStream();

      return await _assetService.UploadAsync(new UploadAssetCommand
      {
        TenantId = tenantId,
        UploadedByUserId = userId,
        Module = "loyalty",
        EntityType = "reward",
        EntityId = rewardId.ToString(),
        AssetType = TenantAssetType.Image,
        Visibility = TenantAssetVisibility.Public,
        OriginalFileName = image.FileName,
        ContentType = image.ContentType,
        SizeBytes = image.Length,
        Content = stream,
        SetAsPrimary = true
      }, ct);
    }

    private async Task DeleteUploadedAssetBestEffortAsync(Guid tenantId, Guid? assetId, CancellationToken ct)
    {
      if (tenantId == Guid.Empty || !assetId.HasValue)
      {
        return;
      }

      try
      {
        await _assetService.DeleteSingleAsync(tenantId, assetId.Value, ct);
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "No fue posible eliminar el asset {AssetId} para tenant {TenantId}", assetId.Value, tenantId);
      }
    }

    private async Task DeleteRewardBestEffortAsync(Guid rewardId, CancellationToken ct)
    {
      try
      {
        await _rewardsService.DeleteRewardAsync(rewardId, ct);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "No fue posible revertir la recompensa {RewardId} tras fallo de imagen", rewardId);
      }
    }
  }

  public sealed class CreateLoyaltyRewardFormRequest
  {
    [FromForm(Name = "name")]
    public string Name { get; init; } = string.Empty;

    [FromForm(Name = "description")]
    public string? Description { get; init; }

    [FromForm(Name = "pointsCost")]
    public int PointsCost { get; init; }

    [FromForm(Name = "rewardType")]
    public string RewardType { get; init; } = string.Empty;

    [FromForm(Name = "productIds")]
    public List<Guid>? ProductIds { get; init; }

    [FromForm(Name = "appliesToAllEligibleProducts")]
    public bool AppliesToAllEligibleProducts { get; init; } = true;

    [FromForm(Name = "singleProductSelectionRule")]
    public string? SingleProductSelectionRule { get; init; }

    [FromForm(Name = "discountValue")]
    public decimal? DiscountValue { get; init; }

    [FromForm(Name = "image")]
    public IFormFile? Image { get; init; }

    [FromForm(Name = "isActive")]
    public bool IsActive { get; init; } = true;

    [FromForm(Name = "stock")]
    public int? Stock { get; init; }

    [FromForm(Name = "validityDays")]
    public int? ValidityDays { get; init; }

    [FromForm(Name = "couponQuantity")]
    public int? CouponQuantity { get; init; }

    [FromForm(Name = "availableFrom")]
    public DateTime? AvailableFrom { get; init; }

    [FromForm(Name = "availableUntil")]
    public DateTime? AvailableUntil { get; init; }

    [FromForm(Name = "displayOrder")]
    public int DisplayOrder { get; init; }
  }

  public sealed class UpdateLoyaltyRewardFormRequest
  {
    [FromForm(Name = "name")]
    public string Name { get; init; } = string.Empty;

    [FromForm(Name = "description")]
    public string? Description { get; init; }

    [FromForm(Name = "pointsCost")]
    public int PointsCost { get; init; }

    [FromForm(Name = "rewardType")]
    public string RewardType { get; init; } = string.Empty;

    [FromForm(Name = "productIds")]
    public List<Guid>? ProductIds { get; init; }

    [FromForm(Name = "appliesToAllEligibleProducts")]
    public bool AppliesToAllEligibleProducts { get; init; } = true;

    [FromForm(Name = "singleProductSelectionRule")]
    public string? SingleProductSelectionRule { get; init; }

    [FromForm(Name = "discountValue")]
    public decimal? DiscountValue { get; init; }

    [FromForm(Name = "image")]
    public IFormFile? Image { get; init; }

    [FromForm(Name = "isActive")]
    public bool IsActive { get; init; }

    [FromForm(Name = "stock")]
    public int? Stock { get; init; }

    [FromForm(Name = "validityDays")]
    public int? ValidityDays { get; init; }

    [FromForm(Name = "couponQuantity")]
    public int? CouponQuantity { get; init; }

    [FromForm(Name = "availableFrom")]
    public DateTime? AvailableFrom { get; init; }

    [FromForm(Name = "availableUntil")]
    public DateTime? AvailableUntil { get; init; }

    [FromForm(Name = "displayOrder")]
    public int DisplayOrder { get; init; }
  }
}
