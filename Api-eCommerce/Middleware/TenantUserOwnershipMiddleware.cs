using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Api_eCommerce.Middleware
{
  /// <summary>
  /// Middleware que valida que el usuario autenticado pertenece al tenant resuelto
  /// Previene que un usuario de tenant A acceda a datos de tenant B
  /// </summary>
  public class TenantUserOwnershipMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantUserOwnershipMiddleware> _logger;

    private static readonly string[] ExcludedPaths =
    {
            "/swagger",
            "/health",
            "/admin",           // SuperAdmin endpoints (AdminDb)
            "/provision",       // Provisioning (AdminDb)
            "/superadmin",      // SuperAdmin management
            "/_framework",
            "/_vs",
            "/webhooks"         // Webhooks no requieren tenant ownership
        };

    public TenantUserOwnershipMiddleware(
        RequestDelegate next,
        ILogger<TenantUserOwnershipMiddleware> logger)
    {
      _next = next;
      _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantAccessor tenantAccessor)
    {
      // 1. Verificar si la ruta está excluida
      if (IsExcludedPath(context.Request.Path))
      {
        await _next(context);
        return;
      }

      // 2. Solo validar si hay tenant resuelto Y el usuario está autenticado
      if (!tenantAccessor.HasTenant || !context.User.Identity?.IsAuthenticated == true)
      {
        // Guest checkout, public endpoints - OK
        await _next(context);
        return;
      }

      // 3. Validar tenant_id claim vs tenant actual
      var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;

      if (string.IsNullOrEmpty(tenantIdClaim))
      {
        // Token antiguo sin tenant_id - por compatibilidad, permitir (log warning)
        _logger.LogWarning(
            "⚠️ JWT without tenant_id claim for user {UserId} on tenant {TenantSlug}. " +
            "This is allowed for backward compatibility but should be regenerated.",
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            tenantAccessor.TenantInfo?.Slug);

        await _next(context);
        return;
      }

      // 4. Validar que el tenant del JWT coincide con el tenant resuelto
      if (!Guid.TryParse(tenantIdClaim, out var jwtTenantId))
      {
        _logger.LogError(
            "🔴 Invalid tenant_id claim format: {TenantIdClaim}",
            tenantIdClaim);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
          error = "Invalid Token",
          detail = "Token contains invalid tenant identifier"
        });
        return;
      }

      if (jwtTenantId != tenantAccessor.TenantInfo!.Id)
      {
        _logger.LogWarning(
            "🔴 Tenant mismatch! User {UserId} with JWT tenant {JwtTenant} ({JwtTenantSlug}) " +
            "attempted to access tenant {CurrentTenant} ({CurrentTenantSlug}). Path: {Path}",
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            jwtTenantId,
            context.User.FindFirst("tenant_slug")?.Value ?? "unknown",
            tenantAccessor.TenantInfo.Id,
            tenantAccessor.TenantInfo.Slug,
            context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
          error = "Token Tenant Mismatch",
          detail = "Your authentication token belongs to a different tenant. Please re-authenticate.",
          jwtTenant = context.User.FindFirst("tenant_slug")?.Value ?? jwtTenantId.ToString(),
          requestedTenant = tenantAccessor.TenantInfo.Slug
        });
        return;
      }

      // ✅ Validación exitosa - el usuario pertenece al tenant correcto
      await _next(context);
    }

    private bool IsExcludedPath(PathString path)
    {
      var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
      return ExcludedPaths.Any(excluded => pathValue.StartsWith(excluded.ToLowerInvariant()));
    }
  }

  /// <summary>
  /// Extension method para registrar el middleware
  /// </summary>
  public static class TenantUserOwnershipMiddlewareExtensions
  {
    public static IApplicationBuilder UseTenantUserOwnershipValidation(this IApplicationBuilder builder)
    {
      return builder.UseMiddleware<TenantUserOwnershipMiddleware>();
    }
  }
}
