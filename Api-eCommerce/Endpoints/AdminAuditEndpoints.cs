using CC.Aplication.Admin;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;

namespace Api_eCommerce.Endpoints
{
  public static class AdminAuditEndpoints
  {
    public static void MapAdminAuditEndpoints(this IEndpointRouteBuilder app)
    {
      var adminAudit = app.MapGroup("/admin/audit")
          .RequireAuthorization()
          .AddEndpointFilter<Authorization.AdminRoleAuthorizationFilter>()
          .WithTags("Admin Audit");

      // ==================== GET: Listar logs de auditoría ====================
      adminAudit.MapGet("/", async (
          [FromServices] IAdminAuditService auditService,
          [AsParameters] AuditLogQuery query,
          CancellationToken ct) =>
      {
        try
        {
          var result = await auditService.GetAuditLogsAsync(query, ct);
          return Results.Ok(result);
        }
        catch (Exception ex)
        {
          return Results.Problem(
                    statusCode: 500,
                    title: "Error retrieving audit logs",
                    detail: ex.Message
                );
        }
      })
      .WithName("GetAuditLogs")
      .WithMetadata(new Authorization.RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin))
      .Produces<PagedAuditLogsResponse>()
      .Produces(500);

      // ==================== GET: Logs de un usuario específico ====================
      adminAudit.MapGet("/user/{userId:guid}", async (
          [FromServices] IAdminAuditService auditService,
          Guid userId,
          [FromQuery] int limit = 100,
          CancellationToken ct = default) =>
      {
        try
        {
          var logs = await auditService.GetUserAuditLogsAsync(userId, limit, ct);
          return Results.Ok(logs);
        }
        catch (Exception ex)
        {
          return Results.Problem(
                    statusCode: 500,
                    title: "Error retrieving user audit logs",
                    detail: ex.Message
                );
        }
      })
      .WithName("GetUserAuditLogs")
      .WithMetadata(new Authorization.RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin))
      .Produces<List<AuditLogDto>>()
      .Produces(500);

      // ==================== GET: Logs de un recurso específico ====================
      adminAudit.MapGet("/resource/{resourceType}/{resourceId}", async (
          [FromServices] IAdminAuditService auditService,
          string resourceType,
          string resourceId,
          [FromQuery] int limit = 100,
          CancellationToken ct = default) =>
      {
        try
        {
          var logs = await auditService.GetResourceAuditLogsAsync(resourceType, resourceId, limit, ct);
          return Results.Ok(logs);
        }
        catch (Exception ex)
        {
          return Results.Problem(
                    statusCode: 500,
                    title: "Error retrieving resource audit logs",
                    detail: ex.Message
                );
        }
      })
      .WithName("GetResourceAuditLogs")
      .WithMetadata(new Authorization.RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin))
      .Produces<List<AuditLogDto>>()
      .Produces(500);
    }

    /// <summary>
    /// Helper para extraer información del usuario actual desde ClaimsPrincipal
    /// </summary>
    public static (Guid userId, string email) GetCurrentAdminUser(ClaimsPrincipal user)
    {
      var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
          ?? user.FindFirst("sub")?.Value
          ?? throw new UnauthorizedAccessException("User ID not found in claims");

      var email = user.FindFirst(ClaimTypes.Email)?.Value
          ?? user.FindFirst("email")?.Value
          ?? throw new UnauthorizedAccessException("Email not found in claims");

      return (Guid.Parse(userIdClaim), email);
    }

    /// <summary>
    /// Helper para extraer IP address desde HttpContext
    /// </summary>
    public static string? GetIpAddress(HttpContext context)
    {
      // Intentar obtener IP real si está detrás de un proxy
      var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
      if (!string.IsNullOrEmpty(forwardedFor))
      {
        return forwardedFor.Split(',')[0].Trim();
      }

      return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Helper para extraer User Agent desde HttpContext
    /// </summary>
    public static string? GetUserAgent(HttpContext context)
    {
      return context.Request.Headers["User-Agent"].FirstOrDefault();
    }
  }
}
