using CC.Domain.Dto;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Roles;

public class RoleService : IRoleService
{
  private readonly TenantDbContextFactory _dbFactory;
  private readonly ITenantAccessor _tenantAccessor;
  private readonly ILogger<RoleService> _logger;

  // Roles del sistema que NO pueden ser eliminados ni renombrados
  private static readonly HashSet<string> SystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuperAdmin",
        "Customer"
    };

  public RoleService(
      TenantDbContextFactory dbFactory,
      ITenantAccessor tenantAccessor,
      ILogger<RoleService> logger)
  {
    _dbFactory = dbFactory;
    _tenantAccessor = tenantAccessor;
    _logger = logger;
  }

  // ==================== CRUD ROLES ====================

  public async Task<RolesResponse> GetRolesAsync(CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var roles = await db.Roles
        .Include(r => r.UserRoles)
        .Include(r => r.ModulePermissions)
        .OrderBy(r => r.Name)
        .ToListAsync(ct);

    var result = roles.Select(r => new RoleListItemDto(
            r.Id,
            r.Name,
            r.Description,
            r.UserRoles.Count,
            r.ModulePermissions.Count(mp => mp.CanView || mp.CanCreate || mp.CanUpdate || mp.CanDelete),
            r.CreatedAt
        ))
        .ToList();

    return new RolesResponse(result);
  }

  public async Task<RoleDetailDto> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var role = await db.Roles
        .Include(r => r.ModulePermissions)
            .ThenInclude(mp => mp.Module)
        .Include(r => r.UserRoles)
            .ThenInclude(ur => ur.User)
        .FirstOrDefaultAsync(r => r.Id == roleId, ct);

    if (role == null)
      throw new InvalidOperationException("Role not found");

    var permissions = role.ModulePermissions
        .Where(mp => mp.Module.IsActive)
        .Select(mp => new RoleModulePermissionDto(
            mp.Module.Id,
            mp.Module.Code,
            mp.Module.Name,
            mp.Module.IconName,
            mp.CanView,
            mp.CanCreate,
            mp.CanUpdate,
            mp.CanDelete
        ))
        .OrderBy(mp => mp.ModuleName)
        .ToList();

    var users = role.UserRoles
        .Select(ur => new RoleUserDto(ur.User.Id, ur.User.Email))
        .OrderBy(u => u.Email)
        .ToList();

    return new RoleDetailDto(
        role.Id,
        role.Name,
        role.Description,
        permissions,
        users,
        role.CreatedAt
    );
  }

  public async Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    // Validar que el nombre no esté vacío
    if (string.IsNullOrWhiteSpace(request.Name))
      throw new InvalidOperationException("Role name is required");

    // Validar que no exista otro rol con el mismo nombre
    var exists = await db.Roles.AnyAsync(r => r.Name == request.Name.Trim(), ct);
    if (exists)
      throw new InvalidOperationException($"Role '{request.Name}' already exists");

    var role = new Role
    {
      Id = Guid.NewGuid(),
      Name = request.Name.Trim(),
      Description = request.Description?.Trim(),
      CreatedAt = DateTime.UtcNow
    };

    db.Roles.Add(role);
    await db.SaveChangesAsync(ct);

    _logger.LogInformation("Role created: {RoleName} (ID: {RoleId})", role.Name, role.Id);

    return await GetRoleByIdAsync(role.Id, ct);
  }

  public async Task<RoleDetailDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var role = await db.Roles.FindAsync(new object[] { roleId }, ct);
    if (role == null)
      throw new InvalidOperationException("Role not found");

    // Validar que el nombre no esté vacío
    if (string.IsNullOrWhiteSpace(request.Name))
      throw new InvalidOperationException("Role name is required");

    // No permitir renombrar roles del sistema
    if (SystemRoles.Contains(role.Name) && role.Name != request.Name.Trim())
      throw new InvalidOperationException($"Cannot rename system role '{role.Name}'");

    // Validar que el nuevo nombre no exista (si cambió)
    if (role.Name != request.Name.Trim())
    {
      var exists = await db.Roles.AnyAsync(r => r.Name == request.Name.Trim() && r.Id != roleId, ct);
      if (exists)
        throw new InvalidOperationException($"Role '{request.Name}' already exists");
    }

    role.Name = request.Name.Trim();
    role.Description = request.Description?.Trim();

    await db.SaveChangesAsync(ct);

    _logger.LogInformation("Role updated: {RoleName} (ID: {RoleId})", role.Name, role.Id);

    return await GetRoleByIdAsync(roleId, ct);
  }

  public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var role = await db.Roles
        .Include(r => r.UserRoles)
        .Include(r => r.ModulePermissions)
        .FirstOrDefaultAsync(r => r.Id == roleId, ct);

    if (role == null)
      throw new InvalidOperationException("Role not found");

    // Protección: no eliminar roles del sistema
    if (SystemRoles.Contains(role.Name))
      throw new InvalidOperationException($"Cannot delete system role '{role.Name}'");

    // Protección: no eliminar roles con usuarios asignados
    if (role.UserRoles.Any())
      throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it has {role.UserRoles.Count} user(s) assigned");

    // Eliminar permisos asociados primero
    db.RoleModulePermissions.RemoveRange(role.ModulePermissions);

    // Eliminar el rol
    db.Roles.Remove(role);
    await db.SaveChangesAsync(ct);

    _logger.LogInformation("Role deleted: {RoleName} (ID: {RoleId})", role.Name, role.Id);
  }

  // ==================== GESTIÓN DE PERMISOS ====================

  public async Task<AvailableModulesResponse> GetAvailableModulesAsync(CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var modules = await db.Modules
        .Where(m => m.IsActive)
        .Select(m => new AvailableModuleDto(
            m.Id,
            m.Code,
            m.Name,
            m.Description,
            m.IconName,
            m.IsActive
        ))
        .OrderBy(m => m.Name)
        .ToListAsync(ct);

    return new AvailableModulesResponse(modules);
  }

  public async Task<RolePermissionsResponse> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var role = await db.Roles
        .Include(r => r.ModulePermissions)
            .ThenInclude(mp => mp.Module)
        .FirstOrDefaultAsync(r => r.Id == roleId, ct);

    if (role == null)
      throw new InvalidOperationException("Role not found");

    var permissions = role.ModulePermissions
        .Where(mp => mp.Module.IsActive)
        .Select(mp => new RoleModulePermissionDto(
            mp.Module.Id,
            mp.Module.Code,
            mp.Module.Name,
            mp.Module.IconName,
            mp.CanView,
            mp.CanCreate,
            mp.CanUpdate,
            mp.CanDelete
        ))
        .OrderBy(mp => mp.ModuleName)
        .ToList();

    return new RolePermissionsResponse(role.Id, role.Name, permissions);
  }

  public async Task<RolePermissionsResponse> UpdateRolePermissionsAsync(
      Guid roleId,
      UpdateRolePermissionsRequest request,
      CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var role = await db.Roles
        .Include(r => r.ModulePermissions)
        .FirstOrDefaultAsync(r => r.Id == roleId, ct);

    if (role == null)
      throw new InvalidOperationException("Role not found");

    // Validar que todos los módulos existan
    var moduleIds = request.Permissions.Select(p => p.ModuleId).ToList();
    var existingModules = await db.Modules
        .Where(m => moduleIds.Contains(m.Id))
        .Select(m => m.Id)
        .ToListAsync(ct);

    var missingModules = moduleIds.Except(existingModules).ToList();
    if (missingModules.Any())
      throw new InvalidOperationException($"Modules not found: {string.Join(", ", missingModules)}");

    // Eliminar permisos actuales
    db.RoleModulePermissions.RemoveRange(role.ModulePermissions);

    // Agregar nuevos permisos (solo si tiene al menos un permiso activo)
    foreach (var perm in request.Permissions)
    {
      if (perm.CanView || perm.CanCreate || perm.CanUpdate || perm.CanDelete)
      {
        var newPermission = new RoleModulePermission
        {
          Id = Guid.NewGuid(),
          RoleId = roleId,
          ModuleId = perm.ModuleId,
          CanView = perm.CanView,
          CanCreate = perm.CanCreate,
          CanUpdate = perm.CanUpdate,
          CanDelete = perm.CanDelete,
          CreatedAt = DateTime.UtcNow
        };

        db.RoleModulePermissions.Add(newPermission);
      }
    }

    await db.SaveChangesAsync(ct);

    _logger.LogInformation("Permissions updated for role: {RoleName} (ID: {RoleId})", role.Name, role.Id);

    return await GetRolePermissionsAsync(roleId, ct);
  }

  // ==================== VALIDACIONES ====================

  public async Task<bool> CanDeleteRoleAsync(Guid roleId, CancellationToken ct = default)
  {
    ValidateTenantContext();
    await using var db = _dbFactory.Create();

    var role = await db.Roles
        .Include(r => r.UserRoles)
        .FirstOrDefaultAsync(r => r.Id == roleId, ct);

    if (role == null)
      return false;

    if (SystemRoles.Contains(role.Name))
      return false;

    if (role.UserRoles.Any())
      return false;

    return true;
  }

  public Task<bool> IsSystemRoleAsync(string roleName, CancellationToken ct = default)
  {
    return Task.FromResult(SystemRoles.Contains(roleName));
  }

  // ==================== HELPERS ====================

  private void ValidateTenantContext()
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
      throw new InvalidOperationException("No tenant context available");
  }
}
