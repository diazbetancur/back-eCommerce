using Api_eCommerce.Authorization;
using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers;

[ApiController]
[Route("api/admin/popups")]
[Tags("Popups - Admin")]
[Authorize]
public class PopupController : ControllerBase
{
  private const string TenantNotResolvedTitle = "Tenant Not Resolved";
  private const string TenantNotResolvedDetail = "Unable to resolve tenant from request";

  private readonly IPopupManagementService _popupService;
  private readonly ITenantResolver _tenantResolver;

  public PopupController(
      IPopupManagementService popupService,
      ITenantResolver tenantResolver)
  {
    _popupService = popupService;
    _tenantResolver = tenantResolver;
  }

  [HttpGet]
  [RequireModule("catalog", "view")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [ProducesResponseType<PopupListResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetAll(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20,
      [FromQuery] bool? isActive = null,
      CancellationToken ct = default)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: TenantNotResolvedTitle,
          detail: TenantNotResolvedDetail);
    }

    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 100) pageSize = 20;

    var result = await _popupService.GetAllAsync(page, pageSize, isActive, ct);
    return Ok(result);
  }

  [HttpGet("{id:guid}")]
  [RequireModule("catalog", "view")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [ProducesResponseType<PopupResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: TenantNotResolvedTitle,
          detail: TenantNotResolvedDetail);
    }

    var popup = await _popupService.GetByIdAsync(id, ct);
    return popup == null ? NotFound(new { message = "Popup no encontrado" }) : Ok(popup);
  }

  [HttpPost]
  [RequireModule("catalog", "create")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [Consumes("multipart/form-data")]
  [ProducesResponseType<PopupResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Create([FromForm] CreatePopupFormRequest request, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: TenantNotResolvedTitle,
          detail: TenantNotResolvedDetail);
    }

    if (request.Image is null || request.Image.Length == 0)
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["image"] = new[] { "Image is required" } }
      });
    }

    try
    {
      await using var imageStream = request.Image?.OpenReadStream();

      var popup = await _popupService.CreateAsync(new CreatePopupRequest
      {
        TargetUrl = request.TargetUrl,
        ButtonText = request.ButtonText,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        IsActive = request.IsActive,
        UploadedByUserId = User.FindFirst("sub")?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("email")?.Value
            ?? "system",
        ImageFileName = request.Image?.FileName,
        ImageContentType = request.Image?.ContentType,
        ImageSizeBytes = request.Image?.Length,
        ImageContent = imageStream
      }, ct);

      return CreatedAtAction(nameof(GetById), new { id = popup.Id }, popup);
    }
    catch (InvalidOperationException ex)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Validation Error",
          detail: ex.Message);
    }
  }

  [HttpPut("{id:guid}")]
  [RequireModule("catalog", "update")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [Consumes("multipart/form-data")]
  [ProducesResponseType<PopupResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Update(Guid id, [FromForm] UpdatePopupFormRequest request, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: TenantNotResolvedTitle,
          detail: TenantNotResolvedDetail);
    }

    if (request.Image is null || request.Image.Length == 0)
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["image"] = new[] { "Image is required" } }
      });
    }

    try
    {
      await using var imageStream = request.Image?.OpenReadStream();

      var popup = await _popupService.UpdateAsync(id, new UpdatePopupRequest
      {
        TargetUrl = request.TargetUrl,
        ButtonText = request.ButtonText,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        IsActive = request.IsActive,
        UploadedByUserId = User.FindFirst("sub")?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("email")?.Value
            ?? "system",
        ImageFileName = request.Image?.FileName,
        ImageContentType = request.Image?.ContentType,
        ImageSizeBytes = request.Image?.Length,
        ImageContent = imageStream
      }, ct);

      return Ok(popup);
    }
    catch (InvalidOperationException ex)
    {
      var statusCode = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
          ? StatusCodes.Status404NotFound
          : StatusCodes.Status400BadRequest;

      return Problem(
          statusCode: statusCode,
          title: statusCode == StatusCodes.Status404NotFound ? "Not Found" : "Validation Error",
          detail: ex.Message);
    }
  }

  [HttpDelete("{id:guid}")]
  [RequireModule("catalog", "delete")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: TenantNotResolvedTitle,
          detail: TenantNotResolvedDetail);
    }

    try
    {
      await _popupService.DeleteAsync(id, ct);
      return NoContent();
    }
    catch (InvalidOperationException ex)
    {
      return Problem(
          statusCode: StatusCodes.Status404NotFound,
          title: "Not Found",
          detail: ex.Message);
    }
  }
}

public sealed class CreatePopupFormRequest
{
  [FromForm(Name = "targetUrl")]
  public string? TargetUrl { get; init; }

  [FromForm(Name = "buttonText")]
  public string? ButtonText { get; init; }

  [FromForm(Name = "startDate")]
  public DateTime? StartDate { get; init; }

  [FromForm(Name = "endDate")]
  public DateTime? EndDate { get; init; }

  [FromForm(Name = "isActive")]
  public bool IsActive { get; init; } = false;

  [FromForm(Name = "image")]
  public IFormFile? Image { get; init; }
}

public sealed class UpdatePopupFormRequest
{
  [FromForm(Name = "targetUrl")]
  public string? TargetUrl { get; init; }

  [FromForm(Name = "buttonText")]
  public string? ButtonText { get; init; }

  [FromForm(Name = "startDate")]
  public DateTime? StartDate { get; init; }

  [FromForm(Name = "endDate")]
  public DateTime? EndDate { get; init; }

  [FromForm(Name = "isActive")]
  public bool IsActive { get; init; } = false;

  [FromForm(Name = "image")]
  public IFormFile? Image { get; init; }
}
