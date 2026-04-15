using Api_eCommerce.Authorization;
using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers;

[ApiController]
[Route("api/admin/banners")]
[Tags("Banners - Admin")]
[Authorize]
public class BannerController : ControllerBase
{
  private readonly IBannerManagementService _bannerService;
  private readonly ITenantResolver _tenantResolver;

  public BannerController(
      IBannerManagementService bannerService,
      ITenantResolver tenantResolver)
  {
    _bannerService = bannerService;
    _tenantResolver = tenantResolver;
  }

  [HttpGet]
  [RequireModule("catalog", "view")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [ProducesResponseType<BannerListResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> GetAll(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20,
      [FromQuery] string? search = null,
      [FromQuery] BannerPosition? position = null,
      [FromQuery] bool? isActive = null,
      CancellationToken ct = default)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Tenant no resuelto",
          detail: "No se pudo resolver el tenant para la solicitud.");
    }

    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 100) pageSize = 20;

    var result = await _bannerService.GetAllAsync(page, pageSize, search, position, isActive, ct);
    return Ok(result);
  }

  [HttpGet("{id:guid}")]
  [RequireModule("catalog", "view")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [ProducesResponseType<BannerResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Tenant no resuelto",
          detail: "No se pudo resolver el tenant para la solicitud.");
    }

    var banner = await _bannerService.GetByIdAsync(id, ct);
    return banner == null ? NotFound(new { message = "Banner no encontrado" }) : Ok(banner);
  }

  [HttpPost]
  [RequireModule("catalog", "create")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [Consumes("multipart/form-data")]
  [ProducesResponseType<BannerResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Create([FromForm] CreateBannerFormRequest request, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Tenant no resuelto",
          detail: "No se pudo resolver el tenant para la solicitud.");
    }

    if (string.IsNullOrWhiteSpace(request.Title))
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["title"] = new[] { "El título es obligatorio" } }
      });
    }

    if (request.Image is null || request.Image.Length == 0)
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["image"] = new[] { "La imagen es obligatoria" } }
      });
    }

    try
    {
      await using var imageStream = request.Image.OpenReadStream();

      var banner = await _bannerService.CreateAsync(new CreateBannerRequest
      {
        Title = request.Title,
        Subtitle = request.Subtitle,
        TargetUrl = request.TargetUrl,
        ButtonText = request.ButtonText,
        Position = request.Position,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        DisplayOrder = request.DisplayOrder,
        IsActive = request.IsActive,
        UploadedByUserId = User.FindFirst("sub")?.Value
            ?? User.FindFirst("id")?.Value
            ?? User.FindFirst("email")?.Value
            ?? "system",
        ImageFileName = request.Image.FileName,
        ImageContentType = request.Image.ContentType,
        ImageSizeBytes = request.Image.Length,
        ImageContent = imageStream
      }, ct);

      return CreatedAtAction(nameof(GetById), new { id = banner.Id }, banner);
    }
    catch (InvalidOperationException ex)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Error de validación",
          detail: TranslateBannerError(ex.Message));
    }
  }

  [HttpPut("{id:guid}")]
  [RequireModule("catalog", "update")]
  [ServiceFilter(typeof(ModuleAuthorizationActionFilter))]
  [Consumes("multipart/form-data")]
  [ProducesResponseType<BannerResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Update(Guid id, [FromForm] UpdateBannerFormRequest request, CancellationToken ct)
  {
    var tenant = await _tenantResolver.ResolveAsync(HttpContext);
    if (tenant == null)
    {
      return Problem(
          statusCode: StatusCodes.Status400BadRequest,
          title: "Tenant no resuelto",
          detail: "No se pudo resolver el tenant para la solicitud.");
    }

    if (string.IsNullOrWhiteSpace(request.Title))
    {
      return ValidationProblem(new ValidationProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Errors = new Dictionary<string, string[]> { ["title"] = new[] { "El título es obligatorio" } }
      });
    }

    try
    {
      await using var imageStream = request.Image?.OpenReadStream();

      var banner = await _bannerService.UpdateAsync(id, new UpdateBannerRequest
      {
        Title = request.Title,
        Subtitle = request.Subtitle,
        TargetUrl = request.TargetUrl,
        ButtonText = request.ButtonText,
        Position = request.Position,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        DisplayOrder = request.DisplayOrder,
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

      return Ok(banner);
    }
    catch (InvalidOperationException ex)
    {
      var statusCode = ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no encontrado", StringComparison.OrdinalIgnoreCase)
          ? StatusCodes.Status404NotFound
          : StatusCodes.Status400BadRequest;

      return Problem(
          statusCode: statusCode,
        title: statusCode == StatusCodes.Status404NotFound ? "No encontrado" : "Error de validación",
        detail: TranslateBannerError(ex.Message));
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
          title: "Tenant no resuelto",
          detail: "No se pudo resolver el tenant para la solicitud.");
    }

    try
    {
      await _bannerService.DeleteAsync(id, ct);
      return NoContent();
    }
    catch (InvalidOperationException ex)
    {
      return Problem(
          statusCode: StatusCodes.Status404NotFound,
          title: "No encontrado",
          detail: TranslateBannerError(ex.Message));
    }
  }

  private static string TranslateBannerError(string message)
  {
    if (message.Contains("Banner not found", StringComparison.OrdinalIgnoreCase))
    {
      return "No se encontró el banner solicitado.";
    }

    if (message.Contains("Banner image is required", StringComparison.OrdinalIgnoreCase))
    {
      return "La imagen del banner es obligatoria.";
    }

    if (message.Contains("StartDate must be less than or equal to EndDate", StringComparison.OrdinalIgnoreCase))
    {
      return "La fecha de inicio no puede ser mayor que la fecha de fin.";
    }

    if (message.Contains("Tenant context not available", StringComparison.OrdinalIgnoreCase))
    {
      return "No hay contexto de tenant disponible para procesar la solicitud.";
    }

    return message;
  }
}

public sealed class CreateBannerFormRequest
{
  [FromForm(Name = "title")]
  public string Title { get; init; } = string.Empty;

  [FromForm(Name = "subtitle")]
  public string? Subtitle { get; init; }

  [FromForm(Name = "targetUrl")]
  public string? TargetUrl { get; init; }

  [FromForm(Name = "buttonText")]
  public string? ButtonText { get; init; }

  [FromForm(Name = "position")]
  public BannerPosition Position { get; init; } = BannerPosition.Hero;

  [FromForm(Name = "startDate")]
  public DateTime? StartDate { get; init; }

  [FromForm(Name = "endDate")]
  public DateTime? EndDate { get; init; }

  [FromForm(Name = "displayOrder")]
  public int DisplayOrder { get; init; }

  [FromForm(Name = "isActive")]
  public bool IsActive { get; init; } = true;

  [FromForm(Name = "image")]
  public IFormFile? Image { get; init; }
}

public sealed class UpdateBannerFormRequest
{
  [FromForm(Name = "title")]
  public string Title { get; init; } = string.Empty;

  [FromForm(Name = "subtitle")]
  public string? Subtitle { get; init; }

  [FromForm(Name = "targetUrl")]
  public string? TargetUrl { get; init; }

  [FromForm(Name = "buttonText")]
  public string? ButtonText { get; init; }

  [FromForm(Name = "position")]
  public BannerPosition Position { get; init; } = BannerPosition.Hero;

  [FromForm(Name = "startDate")]
  public DateTime? StartDate { get; init; }

  [FromForm(Name = "endDate")]
  public DateTime? EndDate { get; init; }

  [FromForm(Name = "displayOrder")]
  public int DisplayOrder { get; init; }

  [FromForm(Name = "isActive")]
  public bool IsActive { get; init; } = true;

  [FromForm(Name = "image")]
  public IFormFile? Image { get; init; }
}
