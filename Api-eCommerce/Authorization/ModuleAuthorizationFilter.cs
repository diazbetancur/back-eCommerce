using CC.Aplication.Permissions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api_eCommerce.Authorization
{
    /// <summary>
    /// Endpoint filter que verifica permisos de m�dulos antes de ejecutar el endpoint
    /// </summary>
    public class ModuleAuthorizationFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;

            // Obtener el atributo RequireModule del endpoint
            var moduleAttr = httpContext.GetEndpoint()
                ?.Metadata
                .GetMetadata<RequireModuleAttribute>();

            if (moduleAttr != null)
            {
                // Obtener userId del JWT
                var userId = GetUserIdFromClaims(httpContext.User);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: 401,
                        title: "Unauthorized",
                        detail: "User ID not found in token"
                    );
                }

                // Verificar permisos
                var permissionService = httpContext.RequestServices
                    .GetRequiredService<IPermissionService>();

                var permissions = await permissionService
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
                    return Results.Problem(
                        statusCode: 403,
                        title: "Forbidden",
                        detail: $"No tienes permiso para '{moduleAttr.Permission}' en el m�dulo '{moduleAttr.ModuleCode}'"
                    );
                }
            }

            // Si tiene permiso, continuar con el endpoint
            return await next(context);
        }

        private Guid? GetUserIdFromClaims(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                ?? user.FindFirst("sub")
                ?? user.FindFirst("nameid");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
    }
}
