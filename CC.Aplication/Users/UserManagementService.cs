using CC.Domain.Dto;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Users;

/// <summary>
/// Implementación del servicio de gestión avanzada de usuarios
/// Incluye protección contra lockout de administradores
/// </summary>
public class UserManagementService : IUserManagementService
{
  private readonly TenantDbContextFactory _dbFactory;
  private readonly ITenantAccessor _tenantAccessor;
  private readonly ILogger<UserManagementService> _logger;

  // Roles considerados administrativos (con acceso al panel de admin)
  private static readonly HashSet<string> AdminRoles = new() { "SuperAdmin", "Admin", "Manager" };

  public UserManagementService(
      TenantDbContextFactory dbFactory,
      ITenantAccessor tenantAccessor,
      ILogger<UserManagementService> logger)
  {
    _dbFactory = dbFactory;
    _tenantAccessor = tenantAccessor;
    _logger = logger;
  }

  public async Task<TenantUserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
  {
    var tenantSlug = _tenantAccessor.TenantInfo?.Slug ?? "unknown";
    _logger.LogInformation("Getting user {UserId} details in tenant {Tenant}", userId, tenantSlug);

    await using var db = _dbFactory.Create();

    var user = await db.Users
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .FirstOrDefaultAsync(u => u.Id == userId, ct);

    if (user == null)
    {
      _logger.LogWarning("User {UserId} not found in tenant {Tenant}", userId, tenantSlug);
      return null;
    }

    return new TenantUserDetailDto(
        user.Id,
        user.Email,
        user.FirstName ?? string.Empty,
        user.LastName ?? string.Empty,
        user.PhoneNumber,
        user.UserRoles.Select(ur => ur.Role.Name).ToList(),
        user.IsActive,
        user.MustChangePassword,
        user.CreatedAt,
        user.UpdatedAt
    );
  }

  public async Task<TenantUserDetailDto?> UpdateUserRolesAsync(
      Guid userId,
      UpdateUserRolesRequest request,
      Guid currentAdminUserId,
      CancellationToken ct = default)
  {
    var tenantSlug = _tenantAccessor.TenantInfo?.Slug ?? "unknown";
    _logger.LogInformation(
        "Updating roles for user {UserId} in tenant {Tenant}. Requested roles: {Roles}",
        userId, tenantSlug, string.Join(", ", request.RoleNames));

    await using var db = _dbFactory.Create();

    var user = await db.Users
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .FirstOrDefaultAsync(u => u.Id == userId, ct);

    if (user == null)
    {
      _logger.LogWarning("User {UserId} not found in tenant {Tenant}", userId, tenantSlug);
      return null;
    }

    // ⚠️ PROTECCIÓN DE LOCKOUT: Verificar que el admin no se quite su propio rol de admin
    if (userId == currentAdminUserId)
    {
      var currentAdminRoles = user.UserRoles
          .Select(ur => ur.Role.Name)
          .Where(name => AdminRoles.Contains(name))
          .ToList();

      if (currentAdminRoles.Any())
      {
        // Verificar que los nuevos roles incluyan al menos un rol administrativo
        var hasAdminRoleInNewRoles = request.RoleNames.Any(name => AdminRoles.Contains(name));

        if (!hasAdminRoleInNewRoles)
        {
          _logger.LogWarning(
              "User {UserId} attempted to remove their own admin role in tenant {Tenant}. Operation blocked.",
              userId, tenantSlug);

          throw new InvalidOperationException(
              "No puedes quitarte tu propio rol de administrador. Esto te dejaría sin acceso al panel administrativo.");
        }
      }
    }

    // Verificar que todos los roles existen
    var requestedRoles = await db.Roles
        .Where(r => request.RoleNames.Contains(r.Name))
        .ToListAsync(ct);

    if (requestedRoles.Count != request.RoleNames.Count)
    {
      _logger.LogWarning("Some requested roles not found for user {UserId} in tenant {Tenant}", userId, tenantSlug);
      throw new InvalidOperationException("Uno o más roles especificados no existen");
    }

    // Eliminar roles actuales
    db.UserRoles.RemoveRange(user.UserRoles);

    // Agregar nuevos roles
    var newUserRoles = requestedRoles.Select(role => new UserRole
    {
      UserId = userId,
      RoleId = role.Id,
      AssignedAt = DateTime.UtcNow
    }).ToList();

    db.UserRoles.AddRange(newUserRoles);
    await db.SaveChangesAsync(ct);

    _logger.LogInformation(
        "Successfully updated roles for user {UserId} in tenant {Tenant}. New roles: {Roles}",
        userId, tenantSlug, string.Join(", ", requestedRoles.Select(r => r.Name)));

    // Recargar el usuario con los nuevos roles
    user = await db.Users
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .FirstAsync(u => u.Id == userId, ct);

    return new TenantUserDetailDto(
        user.Id,
        user.Email,
        user.FirstName ?? string.Empty,
        user.LastName ?? string.Empty,
        user.PhoneNumber,
        user.UserRoles.Select(ur => ur.Role.Name).ToList(),
        user.IsActive,
        user.MustChangePassword,
        user.CreatedAt,
        user.UpdatedAt
    );
  }

  public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
  {
    var tenantSlug = _tenantAccessor.TenantInfo?.Slug ?? "unknown";
    _logger.LogInformation("Soft deleting user {UserId} in tenant {Tenant}", userId, tenantSlug);

    await using var db = _dbFactory.Create();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
    if (user == null)
    {
      _logger.LogWarning("User {UserId} not found in tenant {Tenant}", userId, tenantSlug);
      return false;
    }

    // Soft delete: Marcar como inactivo y cambiar email para liberar el constraint
    user.IsActive = false;
    user.Email = $"deleted_{Guid.NewGuid()}@deleted.local";

    await db.SaveChangesAsync(ct);

    _logger.LogInformation("Successfully soft deleted user {UserId} in tenant {Tenant}", userId, tenantSlug);
    return true;
  }

  public async Task<TenantUserDetailDto?> UpdateUserActiveStatusAsync(
      Guid userId,
      UpdateUserActiveStatusRequest request,
      CancellationToken ct = default)
  {
    var tenantSlug = _tenantAccessor.TenantInfo?.Slug ?? "unknown";
    _logger.LogInformation(
        "Updating active status for user {UserId} in tenant {Tenant}. New status: {IsActive}",
        userId, tenantSlug, request.IsActive);

    await using var db = _dbFactory.Create();

    var user = await db.Users
        .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
        .FirstOrDefaultAsync(u => u.Id == userId, ct);

    if (user == null)
    {
      _logger.LogWarning("User {UserId} not found in tenant {Tenant}", userId, tenantSlug);
      return null;
    }

    user.IsActive = request.IsActive;
    await db.SaveChangesAsync(ct);

    _logger.LogInformation(
        "Successfully updated active status for user {UserId} in tenant {Tenant}",
        userId, tenantSlug);

    return new TenantUserDetailDto(
        user.Id,
        user.Email,
        user.FirstName ?? string.Empty,
        user.LastName ?? string.Empty,
        user.PhoneNumber,
        user.UserRoles.Select(ur => ur.Role.Name).ToList(),
        user.IsActive,
        user.MustChangePassword,
        user.CreatedAt,
        user.UpdatedAt
    );
  }

  public async Task<bool> HasAdminRoleAsync(Guid userId, CancellationToken ct = default)
  {
    var tenantSlug = _tenantAccessor.TenantInfo?.Slug ?? "unknown";
    _logger.LogDebug("Checking if user {UserId} has admin role in tenant {Tenant}", userId, tenantSlug);

    await using var db = _dbFactory.Create();

    var hasAdminRole = await db.UserRoles
        .Include(ur => ur.Role)
    .AnyAsync(ur => ur.UserId == userId && AdminRoles.Contains(ur.Role.Name), ct);

    _logger.LogDebug(
        "User {UserId} in tenant {Tenant} has admin role: {HasAdminRole}",
        userId, tenantSlug, hasAdminRole);

    return hasAdminRole;
  }
}
