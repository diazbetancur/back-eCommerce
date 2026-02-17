using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CC.Aplication.Admin
{
  public interface IAdminAuditService
  {
    /// <summary>
    /// Registra una acción de auditoría
    /// </summary>
    Task LogActionAsync(
        Guid adminUserId,
        string adminUserEmail,
        string action,
        string resourceType,
        string? resourceId = null,
        object? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene logs de auditoría con filtros y paginación
    /// </summary>
    Task<PagedAuditLogsResponse> GetAuditLogsAsync(AuditLogQuery query, CancellationToken ct = default);

    /// <summary>
    /// Obtiene logs de auditoría de un usuario específico
    /// </summary>
    Task<List<AuditLogDto>> GetUserAuditLogsAsync(Guid adminUserId, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Obtiene logs de auditoría de un recurso específico
    /// </summary>
    Task<List<AuditLogDto>> GetResourceAuditLogsAsync(string resourceType, string resourceId, int limit = 100, CancellationToken ct = default);
  }

  public class AdminAuditService : IAdminAuditService
  {
    private readonly AdminDbContext _adminDb;
    private readonly ILogger<AdminAuditService> _logger;

    public AdminAuditService(
        AdminDbContext adminDb,
        ILogger<AdminAuditService> logger)
    {
      _adminDb = adminDb;
      _logger = logger;
    }

    public async Task LogActionAsync(
        Guid adminUserId,
        string adminUserEmail,
        string action,
        string resourceType,
        string? resourceId = null,
        object? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
      try
      {
        var detailsJson = details != null
            ? JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = false })
            : null;

        var auditLog = new AdminAuditLog
        {
          AdminUserId = adminUserId,
          AdminUserEmail = adminUserEmail,
          Action = action,
          ResourceType = resourceType,
          ResourceId = resourceId,
          Details = detailsJson,
          IpAddress = ipAddress,
          UserAgent = userAgent,
          CreatedAt = DateTime.UtcNow
        };

        _adminDb.AdminAuditLogs.Add(auditLog);
        await _adminDb.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Audit log created: {Action} on {ResourceType} {ResourceId} by {UserEmail}",
            action, resourceType, resourceId ?? "N/A", adminUserEmail);
      }
      catch (Exception ex)
      {
        // No queremos que falle la operación principal si falla el logging
        _logger.LogError(ex, "Failed to create audit log for action {Action} by {UserEmail}", action, adminUserEmail);
      }
    }

    public async Task<PagedAuditLogsResponse> GetAuditLogsAsync(AuditLogQuery query, CancellationToken ct = default)
    {
      var queryable = _adminDb.AdminAuditLogs.AsQueryable();

      // Filtros
      if (query.AdminUserId.HasValue)
      {
        queryable = queryable.Where(log => log.AdminUserId == query.AdminUserId.Value);
      }

      if (!string.IsNullOrWhiteSpace(query.Action))
      {
        queryable = queryable.Where(log => log.Action == query.Action);
      }

      if (!string.IsNullOrWhiteSpace(query.ResourceType))
      {
        queryable = queryable.Where(log => log.ResourceType == query.ResourceType);
      }

      if (!string.IsNullOrWhiteSpace(query.ResourceId))
      {
        queryable = queryable.Where(log => log.ResourceId == query.ResourceId);
      }

      if (query.StartDate.HasValue)
      {
        queryable = queryable.Where(log => log.CreatedAt >= query.StartDate.Value);
      }

      if (query.EndDate.HasValue)
      {
        queryable = queryable.Where(log => log.CreatedAt <= query.EndDate.Value);
      }

      // Total count antes de paginar
      var totalCount = await queryable.CountAsync(ct);

      // Ordenar por fecha descendente (más reciente primero) y paginar
      var items = await queryable
          .OrderByDescending(log => log.CreatedAt)
          .Skip((query.Page - 1) * query.PageSize)
          .Take(query.PageSize)
          .Select(log => new AuditLogDto(
              log.Id,
              log.AdminUserId,
              log.AdminUserEmail,
              log.Action,
              log.ResourceType,
              log.ResourceId,
              log.Details,
              log.IpAddress,
              log.CreatedAt
          ))
          .ToListAsync(ct);

      var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

      return new PagedAuditLogsResponse(items, totalCount, query.Page, query.PageSize, totalPages);
    }

    public async Task<List<AuditLogDto>> GetUserAuditLogsAsync(Guid adminUserId, int limit = 100, CancellationToken ct = default)
    {
      return await _adminDb.AdminAuditLogs
          .Where(log => log.AdminUserId == adminUserId)
          .OrderByDescending(log => log.CreatedAt)
          .Take(limit)
          .Select(log => new AuditLogDto(
              log.Id,
              log.AdminUserId,
              log.AdminUserEmail,
              log.Action,
              log.ResourceType,
              log.ResourceId,
              log.Details,
              log.IpAddress,
              log.CreatedAt
          ))
          .ToListAsync(ct);
    }

    public async Task<List<AuditLogDto>> GetResourceAuditLogsAsync(string resourceType, string resourceId, int limit = 100, CancellationToken ct = default)
    {
      return await _adminDb.AdminAuditLogs
          .Where(log => log.ResourceType == resourceType && log.ResourceId == resourceId)
          .OrderByDescending(log => log.CreatedAt)
          .Take(limit)
          .Select(log => new AuditLogDto(
              log.Id,
              log.AdminUserId,
              log.AdminUserEmail,
              log.Action,
              log.ResourceType,
              log.ResourceId,
              log.Details,
              log.IpAddress,
              log.CreatedAt
          ))
          .ToListAsync(ct);
    }
  }

  /// <summary>
  /// Tipos de acciones de auditoría predefinidas
  /// </summary>
  public static class AuditActions
  {
    // User Management
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserDeleted = "UserDeleted";
    public const string UserRolesUpdated = "UserRolesUpdated";
    public const string UserPasswordChanged = "UserPasswordChanged";
    public const string UserActivated = "UserActivated";
    public const string UserDeactivated = "UserDeactivated";

    // Tenant Management
    public const string TenantCreated = "TenantCreated";
    public const string TenantUpdated = "TenantUpdated";
    public const string TenantDeleted = "TenantDeleted";
    public const string TenantActivated = "TenantActivated";
    public const string TenantDeactivated = "TenantDeactivated";

    // Authentication
    public const string LoginSuccess = "LoginSuccess";
    public const string LoginFailed = "LoginFailed";
    public const string LogoutSuccess = "LogoutSuccess";

    // Plan Management
    public const string PlanCreated = "PlanCreated";
    public const string PlanUpdated = "PlanUpdated";
    public const string PlanDeleted = "PlanDeleted";
    public const string TenantPlanChanged = "TenantPlanChanged";
  }

  /// <summary>
  /// Tipos de recursos auditables
  /// </summary>
  public static class AuditResourceTypes
  {
    public const string AdminUser = "AdminUser";
    public const string Tenant = "Tenant";
    public const string Plan = "Plan";
    public const string Feature = "Feature";
    public const string Authentication = "Authentication";
  }
}
