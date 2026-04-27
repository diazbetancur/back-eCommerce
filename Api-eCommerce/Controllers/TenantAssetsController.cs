using CC.Aplication.Assets;
using CC.Domain.Assets;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers;

[ApiController]
[Route("api/tenant/assets")]
[Tags("Tenant Assets")]
[Authorize]
public class TenantAssetsController : ControllerBase
{
  private const string TenantNotResolvedMessage = "Tenant not resolved";

  private readonly IAssetService _assetService;
  private readonly ITenantAccessor _tenantAccessor;

  public TenantAssetsController(IAssetService assetService, ITenantAccessor tenantAccessor)
  {
    _assetService = assetService;
    _tenantAccessor = tenantAccessor;
  }

  [HttpPost("upload")]
  [Consumes("multipart/form-data")]
  [ProducesResponseType<TenantAssetDto>(StatusCodes.Status201Created)]
  public async Task<IActionResult> Upload([FromForm] UploadTenantAssetRequest request, CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    if (request.File is null || request.File.Length == 0)
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["file"] = new[] { "File is required" } }
      });
    }

    if (string.IsNullOrWhiteSpace(request.Module))
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["module"] = new[] { "Module is required" } }
      });
    }

    try
    {
      await using var stream = request.File.OpenReadStream();

      var userId = User.FindFirst("sub")?.Value
          ?? User.FindFirst("id")?.Value
          ?? User.FindFirst("email")?.Value
          ?? "system";

      var result = await _assetService.UploadAsync(new UploadAssetCommand
      {
        TenantId = _tenantAccessor.TenantInfo.Id,
        UploadedByUserId = userId,
        Module = request.Module,
        EntityType = request.EntityType,
        EntityId = request.EntityId,
        AssetType = request.AssetType,
        Visibility = request.Visibility,
        OriginalFileName = request.File.FileName,
        ContentType = request.File.ContentType,
        SizeBytes = request.File.Length,
        Content = stream,
        SetAsPrimary = request.SetAsPrimary
      }, ct);

      return CreatedAtAction(nameof(GetByEntity), new
      {
        module = request.Module,
        entityType = request.EntityType,
        entityId = request.EntityId
      }, result);
    }
    catch (InvalidOperationException ex)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid asset upload request", detail: ex.Message);
    }
  }

  [HttpGet("by-entity")]
  [ProducesResponseType<IReadOnlyList<TenantAssetDto>>(StatusCodes.Status200OK)]
  public async Task<IActionResult> GetByEntity([FromQuery] string module, [FromQuery] string entityType, [FromQuery] string entityId, CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
    {
      var errors = new Dictionary<string, string[]>();
      if (string.IsNullOrWhiteSpace(module)) errors["module"] = new[] { "module is required" };
      if (string.IsNullOrWhiteSpace(entityType)) errors["entityType"] = new[] { "entityType is required" };
      if (string.IsNullOrWhiteSpace(entityId)) errors["entityId"] = new[] { "entityId is required" };

      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = errors
      });
    }

    var items = await _assetService.ListByEntityAsync(_tenantAccessor.TenantInfo.Id, module, entityType, entityId, ct);
    return Ok(items);
  }

  [HttpDelete("{assetId:guid}")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  public async Task<IActionResult> Delete(Guid assetId, CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    try
    {
      await _assetService.DeleteSingleAsync(_tenantAccessor.TenantInfo.Id, assetId, ct);
      return NoContent();
    }
    catch (InvalidOperationException ex)
    {
      return Problem(
          statusCode: StatusCodes.Status500InternalServerError,
          title: "Error al eliminar asset",
          detail: ex.Message);
    }
  }

  [HttpPost("{assetId:guid}/set-primary")]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  public async Task<IActionResult> SetPrimary(Guid assetId, CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    try
    {
      await _assetService.SetPrimaryAsync(_tenantAccessor.TenantInfo.Id, assetId, ct);
      return NoContent();
    }
    catch (InvalidOperationException ex)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid set-primary request", detail: ex.Message);
    }
  }

  [HttpPost("purge-entity")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> PurgeEntity([FromBody] PurgeEntityAssetsRequest request, CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.EntityType) || string.IsNullOrWhiteSpace(request.EntityId))
    {
      var errors = new Dictionary<string, string[]>();
      if (string.IsNullOrWhiteSpace(request.Module)) errors["module"] = new[] { "module is required" };
      if (string.IsNullOrWhiteSpace(request.EntityType)) errors["entityType"] = new[] { "entityType is required" };
      if (string.IsNullOrWhiteSpace(request.EntityId)) errors["entityId"] = new[] { "entityId is required" };

      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = errors
      });
    }

    var count = await _assetService.PurgeByEntityAsync(
        _tenantAccessor.TenantInfo.Id,
        request.Module,
        request.EntityType,
        request.EntityId,
        ct);

    return Ok(new { deleted = count });
  }

  [HttpPost("purge-tenant")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> PurgeTenant(CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    var count = await _assetService.PurgeByTenantAsync(_tenantAccessor.TenantInfo.Id, ct);
    return Ok(new { deleted = count });
  }

  [HttpGet("quota-status")]
  [ProducesResponseType<TenantAssetQuotaStatusDto>(StatusCodes.Status200OK)]
  public async Task<IActionResult> QuotaStatus(CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    var status = await _assetService.GetQuotaStatusAsync(_tenantAccessor.TenantInfo.Id, ct);
    return Ok(status);
  }

  [HttpPost("quota-recalculate")]
  [ProducesResponseType(StatusCodes.Status200OK)]
  public async Task<IActionResult> RecalculateQuota(CancellationToken ct)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return Problem(statusCode: StatusCodes.Status400BadRequest, title: TenantNotResolvedMessage);
    }

    await _assetService.RecalculateQuotaAsync(_tenantAccessor.TenantInfo.Id, ct);
    return Ok(new { status = "recalculated" });
  }
}

public sealed class UploadTenantAssetRequest
{
  public IFormFile File { get; init; } = default!;
  public string Module { get; init; } = string.Empty;
  public string? EntityType { get; init; }
  public string? EntityId { get; init; }
  public TenantAssetType AssetType { get; init; } = TenantAssetType.Image;
  public TenantAssetVisibility Visibility { get; init; } = TenantAssetVisibility.Public;
  public bool SetAsPrimary { get; init; }
}

public sealed class PurgeEntityAssetsRequest
{
  public string Module { get; init; } = string.Empty;
  public string EntityType { get; init; } = string.Empty;
  public string EntityId { get; init; } = string.Empty;
}
