using CC.Aplication.Permissions;
using CC.Domain.Dto;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para gestión de módulos y permisos de usuario
  /// </summary>
  [ApiController]
  [Route("me")]
  [Authorize]
  [Tags("User Permissions")]
  public class PermissionsController : ControllerBase
  {
    private readonly IPermissionService _permissionService;
    private readonly ITenantResolver _tenantResolver;

    public PermissionsController(IPermissionService permissionService, ITenantResolver tenantResolver)
    {
      _permissionService = permissionService;
      _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Get user's available modules and permissions
    /// </summary>
    [HttpGet("modules")]
    [Api_eCommerce.Authorization.RequireModule("permissions", "view")]
    [ServiceFilter(typeof(Api_eCommerce.Authorization.ModuleAuthorizationActionFilter))]
    [ProducesResponseType<ModulesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserModules()
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

        // Obtener módulos del usuario
        var modules = await _permissionService.GetUserModulesAsync(userId.Value);

        return Ok(new ModulesResponse
        {
          Modules = modules.Select(m => new ModuleResponse
          {
            Code = m.Code,
            Name = m.Name,
            Description = m.Description,
            IconName = m.IconName,
            Permissions = new PermissionsResponse
            {
              CanView = m.Permissions.CanView,
              CanCreate = m.Permissions.CanCreate,
              CanUpdate = m.Permissions.CanUpdate,
              CanDelete = m.Permissions.CanDelete
            }
          }).ToList()
        });
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
            detail: "An error occurred while retrieving modules"
        );
      }
    }

    /// <summary>
    /// Get user's permissions for a specific module
    /// </summary>
    [HttpGet("modules/{moduleCode}/permissions")]
    [Api_eCommerce.Authorization.RequireModule("permissions", "view")]
    [ServiceFilter(typeof(Api_eCommerce.Authorization.ModuleAuthorizationActionFilter))]
    [ProducesResponseType<ModulePermissions>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModulePermissions(string moduleCode)
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

        // Obtener permisos del módulo
        var permissions = await _permissionService.GetUserPermissionsAsync(userId.Value, moduleCode);

        return Ok(permissions);
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
            detail: "An error occurred while retrieving permissions"
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
