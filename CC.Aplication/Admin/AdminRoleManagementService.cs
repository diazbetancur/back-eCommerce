using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Admin;

public class AdminRoleManagementService : IAdminRoleManagementService
{
    private readonly AdminDbContext _context;
    private readonly ILogger<AdminRoleManagementService> _logger;

    public AdminRoleManagementService(
        AdminDbContext context,
        ILogger<AdminRoleManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==================== ROLES ====================

    public async Task<List<AdminRoleDetailDto>> GetAllRolesAsync()
    {
        var roles = await _context.AdminRoles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.AdminPermission)
            .OrderBy(r => r.Name)
            .ToListAsync();

        return roles.Select(MapToDetailDto).ToList();
    }

    public async Task<AdminRoleDetailDto?> GetRoleByIdAsync(Guid roleId)
    {
        var role = await _context.AdminRoles
            .Include(r => r.UserRoles)
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.AdminPermission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        return role != null ? MapToDetailDto(role) : null;
    }

    public async Task<AdminRoleDetailDto> CreateRoleAsync(CreateAdminRoleRequest request)
    {
        // Validar que no exista un rol con ese nombre
        var existingRole = await _context.AdminRoles
            .FirstOrDefaultAsync(r => r.Name.ToLower() == request.Name.ToLower());

        if (existingRole != null)
        {
            throw new InvalidOperationException($"A role with name '{request.Name}' already exists.");
        }

        var role = new AdminRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsSystemRole = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.AdminRoles.Add(role);
        await _context.SaveChangesAsync();

        // Asignar permisos si se proporcionaron
        if (request.PermissionIds != null && request.PermissionIds.Count > 0)
        {
            await AssignPermissionsToRoleAsync(role.Id, request.PermissionIds, null);
        }

        _logger.LogInformation("Created new admin role: {RoleName} (ID: {RoleId})", role.Name, role.Id);

        return (await GetRoleByIdAsync(role.Id))!;
    }

    public async Task<AdminRoleDetailDto> UpdateRoleAsync(Guid roleId, UpdateAdminRoleRequest request)
    {
        var role = await _context.AdminRoles.FindAsync(roleId);
        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {roleId} not found.");
        }

        // Los roles del sistema no pueden cambiar de nombre (pero sí descripción)
        if (role.IsSystemRole && request.Name != null && request.Name != role.Name)
        {
            throw new InvalidOperationException("Cannot rename system roles. You can update their description.");
        }

        // Validar unicidad de nombre si se está cambiando
        if (request.Name != null && request.Name != role.Name)
        {
            var existingRole = await _context.AdminRoles
                .FirstOrDefaultAsync(r => r.Name.ToLower() == request.Name.ToLower());

            if (existingRole != null)
            {
                throw new InvalidOperationException($"A role with name '{request.Name}' already exists.");
            }

            role.Name = request.Name;
        }

        if (request.Description != null)
        {
            role.Description = request.Description;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated admin role: {RoleName} (ID: {RoleId})", role.Name, role.Id);

        return (await GetRoleByIdAsync(roleId))!;
    }

    public async Task<bool> DeleteRoleAsync(Guid roleId)
    {
        var role = await _context.AdminRoles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {roleId} not found.");
        }

        var (canDelete, reason) = await CanDeleteRoleAsync(roleId);
        if (!canDelete)
        {
            throw new InvalidOperationException(reason ?? "Cannot delete this role.");
        }

        // Eliminar permisos asociados
        var rolePermissions = await _context.AdminRolePermissions
            .Where(rp => rp.AdminRoleId == roleId)
            .ToListAsync();

        _context.AdminRolePermissions.RemoveRange(rolePermissions);

        // Eliminar el rol
        _context.AdminRoles.Remove(role);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Deleted admin role: {RoleName} (ID: {RoleId})", role.Name, role.Id);

        return true;
    }

    public async Task<(bool CanDelete, string? Reason)> CanDeleteRoleAsync(Guid roleId)
    {
        var role = await _context.AdminRoles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            return (false, "Role not found.");
        }

        // No se pueden eliminar roles del sistema
        if (role.IsSystemRole)
        {
            return (false, $"Cannot delete system role '{role.Name}'.");
        }

        // No se puede eliminar si tiene usuarios asignados
        if (role.UserRoles.Any())
        {
            return (false, $"Cannot delete role '{role.Name}' because it has {role.UserRoles.Count} user(s) assigned.");
        }

        return (true, null);
    }

    // ==================== PERMISOS ====================

    public async Task<AvailableAdminPermissionsResponse> GetAvailablePermissionsAsync()
    {
        var permissions = await _context.AdminPermissions
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Action)
            .ToListAsync();

        var groups = permissions
            .GroupBy(p => p.Resource)
            .Select(g => new PermissionGroupDto(
                g.Key,
                g.Select(p => new AdminPermissionDto(
                    p.Id,
                    p.Name,
                    p.Resource,
                    p.Action,
                    p.Description
                )).ToList()
            ))
            .ToList();

        return new AvailableAdminPermissionsResponse(groups);
    }

    public async Task<AdminRolePermissionsResponse> GetRolePermissionsAsync(Guid roleId)
    {
        var role = await _context.AdminRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.AdminPermission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {roleId} not found.");
        }

        var permissions = role.RolePermissions
            .Select(rp => new AdminPermissionDto(
                rp.AdminPermission.Id,
                rp.AdminPermission.Name,
                rp.AdminPermission.Resource,
                rp.AdminPermission.Action,
                rp.AdminPermission.Description
            ))
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Action)
            .ToList();

        return new AdminRolePermissionsResponse(roleId, role.Name, permissions);
    }

    public async Task<AdminRolePermissionsResponse> UpdateRolePermissionsAsync(
        Guid roleId,
        UpdateAdminRolePermissionsRequest request,
        Guid? assignedByUserId = null)
    {
        var role = await _context.AdminRoles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
        {
            throw new KeyNotFoundException($"Role with ID {roleId} not found.");
        }

        await AssignPermissionsToRoleAsync(roleId, request.PermissionIds, assignedByUserId);

        _logger.LogInformation(
            "Updated permissions for role {RoleName} (ID: {RoleId}). New permission count: {Count}",
            role.Name, roleId, request.PermissionIds.Count);

        return await GetRolePermissionsAsync(roleId);
    }

    public async Task EnsureSystemPermissionsAsync()
    {
        // 1. Crear permisos si no existen
        var existingPermissions = await _context.AdminPermissions.ToListAsync();
        var existingNames = existingPermissions.Select(p => p.Name).ToHashSet();

        var systemPermissions = GetSystemPermissions();
        var permissionsToAdd = systemPermissions
            .Where(p => !existingNames.Contains(p.Name))
            .ToList();

        if (permissionsToAdd.Any())
        {
            _context.AdminPermissions.AddRange(permissionsToAdd);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {Count} new system permissions", permissionsToAdd.Count);

            // Recargar permisos después de crearlos
            existingPermissions = await _context.AdminPermissions.ToListAsync();
        }

        // 2. Asignar TODOS los permisos al rol SuperAdmin si no los tiene
        var superAdminRole = await _context.AdminRoles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Name == "SuperAdmin");

        if (superAdminRole != null)
        {
            var existingPermissionIds = superAdminRole.RolePermissions
                .Select(rp => rp.AdminPermissionId)
                .ToHashSet();

            var allPermissions = existingPermissions.Any()
                ? existingPermissions
                : await _context.AdminPermissions.ToListAsync();

            var missingPermissions = allPermissions
                .Where(p => !existingPermissionIds.Contains(p.Id))
                .ToList();

            if (missingPermissions.Any())
            {
                var rolePermissions = missingPermissions.Select(p => new AdminRolePermission
                {
                    AdminRoleId = superAdminRole.Id,
                    AdminPermissionId = p.Id,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = null
                }).ToList();

                _context.AdminRolePermissions.AddRange(rolePermissions);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Assigned {Count} permissions to SuperAdmin role",
                    missingPermissions.Count);
            }
        }
    }

    // ==================== PRIVATE HELPERS ====================

    private static AdminRoleDetailDto MapToDetailDto(AdminRole role)
    {
        return new AdminRoleDetailDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.UserRoles.Count,
            role.RolePermissions.Select(rp => new AdminPermissionDto(
                rp.AdminPermission.Id,
                rp.AdminPermission.Name,
                rp.AdminPermission.Resource,
                rp.AdminPermission.Action,
                rp.AdminPermission.Description
            )).OrderBy(p => p.Resource).ThenBy(p => p.Action).ToList(),
            role.CreatedAt
        );
    }

    private async Task AssignPermissionsToRoleAsync(
        Guid roleId,
        List<Guid> permissionIds,
        Guid? assignedByUserId)
    {
        // Eliminar permisos existentes
        var existingPermissions = await _context.AdminRolePermissions
            .Where(rp => rp.AdminRoleId == roleId)
            .ToListAsync();

        _context.AdminRolePermissions.RemoveRange(existingPermissions);

        // Agregar nuevos permisos
        var newPermissions = permissionIds.Select(permissionId => new AdminRolePermission
        {
            AdminRoleId = roleId,
            AdminPermissionId = permissionId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId
        }).ToList();

        _context.AdminRolePermissions.AddRange(newPermissions);
        await _context.SaveChangesAsync();
    }

    private static List<AdminPermission> GetSystemPermissions()
    {
        var permissionsData = new[]
        {
            // Tenants
            (AdminResources.Tenants, AdminActions.Create, "Create new tenants"),
            (AdminResources.Tenants, AdminActions.View, "View tenant information"),
            (AdminResources.Tenants, AdminActions.Update, "Update tenant details"),
            (AdminResources.Tenants, AdminActions.Delete, "Delete tenants"),
            (AdminResources.Tenants, AdminActions.ManagePlans, "Manage tenant plans"),

            // Users (Admin Users)
            (AdminResources.Users, AdminActions.Create, "Create admin users"),
            (AdminResources.Users, AdminActions.View, "View admin users"),
            (AdminResources.Users, AdminActions.Update, "Update admin users"),
            (AdminResources.Users, AdminActions.Delete, "Delete admin users"),
            (AdminResources.Users, AdminActions.ManageRoles, "Manage admin user roles"),

            // Roles
            (AdminResources.Roles, AdminActions.Create, "Create admin roles"),
            (AdminResources.Roles, AdminActions.View, "View admin roles"),
            (AdminResources.Roles, AdminActions.Update, "Update admin roles"),
            (AdminResources.Roles, AdminActions.Delete, "Delete admin roles"),

            // Audit
            (AdminResources.Audit, AdminActions.View, "View audit logs"),
            (AdminResources.Audit, AdminActions.Export, "Export audit logs"),

            // Plans
            (AdminResources.Plans, AdminActions.Create, "Create subscription plans"),
            (AdminResources.Plans, AdminActions.View, "View subscription plans"),
            (AdminResources.Plans, AdminActions.Update, "Update subscription plans"),
            (AdminResources.Plans, AdminActions.Delete, "Delete subscription plans"),

            // Features
            (AdminResources.Features, AdminActions.Create, "Create features"),
            (AdminResources.Features, AdminActions.View, "View features"),
            (AdminResources.Features, AdminActions.Update, "Update features"),
            (AdminResources.Features, AdminActions.Delete, "Delete features"),

            // System
            (AdminResources.System, AdminActions.ViewMetrics, "View system metrics"),
            (AdminResources.System, AdminActions.ManageConfig, "Manage system configuration"),
        };

        return permissionsData.Select(p => new AdminPermission
        {
            Id = Guid.NewGuid(),
            Name = $"{p.Item1}:{p.Item2}",
            Resource = p.Item1,
            Action = p.Item2,
            Description = p.Item3,
            IsSystemPermission = true,
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }
}
