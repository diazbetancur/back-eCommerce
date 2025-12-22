using CC.Aplication.Permissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Api_eCommerce.Authorization
{
  /// <summary>
  /// Action filter que verifica permisos de módulos antes de ejecutar la acción del controller
  /// </summary>
  public class ModuleAuthorizationActionFilter : IAsyncActionFilter
  {
    private readonly IPermissionService _permissionService;

    public ModuleAuthorizationActionFilter(IPermissionService permissionService)
    {
      _permissionService = permissionService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
      // Obtener el atributo RequireModule de la acción
      var moduleAttr = context.ActionDescriptor.EndpointMetadata
          .OfType<RequireModuleAttribute>()
          .FirstOrDefault();

      if (moduleAttr != null)
      {
        // Obtener userId del JWT
        var userId = GetUserIdFromClaims(context.HttpContext.User);
        if (!userId.HasValue)
        {
          context.Result = new ObjectResult(new ProblemDetails
          {
            Status = 401,
            Title = "Unauthorized",
            Detail = "User ID not found in token"
          })
          {
            StatusCode = 401
          };
          return;
        }

        // Verificar permisos
        var permissions = await _permissionService
            .GetUserPermissionsAsync(userId.Value, moduleAttr.ModuleCode);

        var hasPermission = moduleAttr.Permission switch
        {
          "view" => permissions.CanView,
          "create" => permissions.CanCreate,
          "update" => permissions.CanUpdate,
          "delete" => permissions.CanDelete,
          _ => false
        };

        if (!hasPermission)
        {
          context.Result = new ObjectResult(new ProblemDetails
          {
            Status = 403,
            Title = "Forbidden",
            Detail = $"No tienes permiso para '{moduleAttr.Permission}' en el módulo '{moduleAttr.ModuleCode}'"
          })
          {
            StatusCode = 403
          };
          return;
        }
      }

      // Si tiene permiso, continuar con la acción
      await next();
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
  }
}
