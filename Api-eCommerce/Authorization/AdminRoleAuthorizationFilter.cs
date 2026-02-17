using System.Security.Claims;

namespace Api_eCommerce.Authorization
{
  /// <summary>
  /// Endpoint filter que verifica roles administrativos antes de ejecutar el endpoint.
  /// Similar a ModuleAuthorizationFilter pero para roles del sistema de admin (SuperAdmin, TenantManager, etc.)
  /// </summary>
  public class AdminRoleAuthorizationFilter : IEndpointFilter
  {
    private readonly ILogger<AdminRoleAuthorizationFilter> _logger;

    public AdminRoleAuthorizationFilter(ILogger<AdminRoleAuthorizationFilter> logger)
    {
      _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
      var httpContext = context.HttpContext;
      var endpoint = httpContext.GetEndpoint();

      // Obtener el atributo RequireAdminRole del endpoint
      var roleAttr = endpoint?.Metadata
          .GetMetadata<RequireAdminRoleAttribute>();

      // Si no hay atributo de rol, permitir acceso (solo requiere autenticación)
      if (roleAttr == null || roleAttr.Roles.Length == 0)
      {
        return await next(context);
      }

      // Verificar que el usuario esté autenticado
      if (!httpContext.User.Identity?.IsAuthenticated ?? true)
      {
        _logger.LogWarning("Unauthorized access attempt to {Path}", httpContext.Request.Path);
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "You must be logged in to access this resource"
        );
      }

      // Obtener los roles del usuario desde los claims
      // Buscar tanto en ClaimTypes.Role como en "role" directamente para máxima compatibilidad
      var userRoles = httpContext.User.FindAll(ClaimTypes.Role)
          .Select(c => c.Value)
          .Union(httpContext.User.FindAll("role").Select(c => c.Value))
          .Distinct()
          .ToList();

      // Verificar si el usuario tiene al menos uno de los roles requeridos
      var hasRequiredRole = roleAttr.Roles.Any(required =>
          userRoles.Contains(required, StringComparer.OrdinalIgnoreCase));

      if (!hasRequiredRole)
      {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? httpContext.User.FindFirst("nameid")?.Value
                     ?? httpContext.User.FindFirst("sub")?.Value
                     ?? "Unknown";
        var userEmail = httpContext.User.FindFirst(ClaimTypes.Email)?.Value
                        ?? httpContext.User.FindFirst("email")?.Value
                        ?? "Unknown";

        _logger.LogWarning(
            "Access denied for user {UserId} ({Email}) to {Path}. Required roles: [{RequiredRoles}], User roles: [{UserRoles}]",
            userId,
            userEmail,
            httpContext.Request.Path,
            string.Join(", ", roleAttr.Roles),
            string.Join(", ", userRoles));

        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: $"You need one of these roles to access this resource: {string.Join(", ", roleAttr.Roles)}. Your current roles: {string.Join(", ", userRoles)}",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.3"
        );
      }

      _logger.LogDebug(
          "User {Email} granted access to {Path} with roles [{Roles}]",
          httpContext.User.FindFirst(ClaimTypes.Email)?.Value
              ?? httpContext.User.FindFirst("email")?.Value,
          httpContext.Request.Path,
          string.Join(", ", userRoles));

      // Si tiene al menos uno de los roles requeridos, continuar
      return await next(context);
    }
  }
}
