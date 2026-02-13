# 📋 Plan de Implementación RBAC Completo

**Fecha:** 26 de enero de 2026  
**Arquitectura:** Clean Architecture con Minimal APIs  
**Base de datos:** PostgreSQL (TenantDb por tenant)  
**Versión:** .NET 8

---

## 🎯 Objetivos

Completar el sistema RBAC con:
1. ✅ CRUD completo de Roles
2. ✅ Gestión de permisos por rol
3. ✅ Gestión completa de usuarios (detalle, soft delete, activación)
4. ✅ Protección contra lockout (evitar que admin se quite permisos críticos)

---

## 🏗️ Arquitectura Actual Analizada

### **Entidades Existentes** (`CC.Infraestructure/Tenant/Entities/`)
```
User.cs               ✅ Completo (con IsActive, TenantId)
Role.cs               ✅ Completo (Name, Description, CreatedAt)
Module.cs             ✅ Completo (Code, Name, IsActive)
UserRole.cs           ✅ Tabla pivot User-Role
RoleModulePermission.cs ✅ Permisos de rol sobre módulos (CRUD flags)
```

### **Servicios Existentes**
- `IPermissionService` → Consultas de permisos
- `IUnifiedAuthService` → Login y registro
- `IAdminAuthService` → Login admin global (AdminDb)

### **Endpoints Existentes** (`Api-eCommerce/Endpoints/TenantAdminEndpoints.cs`)
```csharp
✅ GET  /admin/users
✅ POST /admin/users
✅ PATCH /admin/users/{id}/role
❌ GET  /admin/users/{id}          → FALTANTE
❌ DELETE /admin/users/{id}        → FALTANTE
❌ PATCH /admin/users/{id}/active  → FALTANTE
```

---

## 📦 Estructura de Archivos a Crear/Modificar

### **1. DTOs** (`CC.Domain/Dto/`)

#### **Nuevos archivos:**
```
RoleDtos.cs              → DTOs para Roles (Request/Response)
RolePermissionDtos.cs    → DTOs para gestión de permisos
```

#### **Contenido de `RoleDtos.cs`:**
```csharp
// Lista de roles
public record RoleListItemDto(
    Guid Id,
    string Name,
    string? Description,
    int UserCount,      // Cuántos usuarios tienen este rol
    int PermissionCount, // Cuántos permisos tiene
    DateTime CreatedAt
);

public record RolesResponse(List<RoleListItemDto> Roles);

// Detalle de rol
public record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    List<RoleModulePermissionDto> Permissions,
    List<RoleUserDto> Users,
    DateTime CreatedAt
);

public record RoleUserDto(Guid UserId, string Email);

// Crear/editar rol
public record CreateRoleRequest(
    string Name,
    string? Description
);

public record UpdateRoleRequest(
    string Name,
    string? Description
);

// Permisos de rol sobre un módulo
public record RoleModulePermissionDto(
    Guid ModuleId,
    string ModuleCode,
    string ModuleName,
    string? IconName,
    bool CanView,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete
);
```

#### **Contenido de `RolePermissionDtos.cs`:**
```csharp
// Catálogo completo de módulos disponibles
public record AvailableModulesResponse(List<AvailableModuleDto> Modules);

public record AvailableModuleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? IconName,
    bool IsActive
);

// Permisos actuales de un rol
public record RolePermissionsResponse(
    Guid RoleId,
    string RoleName,
    List<RoleModulePermissionDto> Permissions
);

// Actualizar permisos de un rol (reemplazo completo)
public record UpdateRolePermissionsRequest(
    List<UpdateModulePermissionDto> Permissions
);

public record UpdateModulePermissionDto(
    Guid ModuleId,
    bool CanView,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete
);
```

#### **Modificar `UserDto.cs`** (si no existe, crear):
```csharp
// Ya existe TenantUserListItemDto, agregar:

public record TenantUserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    List<string> Roles,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record UpdateUserRolesRequest(List<string> RoleNames);

public record UpdateUserActiveStatusRequest(bool IsActive);
```

---

### **2. Servicios** (`CC.Aplication/`)

#### **Crear carpeta `Roles/`** con:
```
IRoleService.cs
RoleService.cs
```

#### **Contenido de `IRoleService.cs`:**
```csharp
namespace CC.Aplication.Roles;

public interface IRoleService
{
    // CRUD Roles
    Task<RolesResponse> GetRolesAsync(CancellationToken ct = default);
    Task<RoleDetailDto> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default);
    Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDetailDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);

    // Gestión de permisos
    Task<AvailableModulesResponse> GetAvailableModulesAsync(CancellationToken ct = default);
    Task<RolePermissionsResponse> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task<RolePermissionsResponse> UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken ct = default);

    // Validaciones
    Task<bool> CanDeleteRoleAsync(Guid roleId, CancellationToken ct = default);
    Task<bool> IsSystemRoleAsync(string roleName, CancellationToken ct = default);
}
```

#### **Contenido de `RoleService.cs`:**
```csharp
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

    // Roles del sistema que NO pueden ser eliminados
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

    public async Task<RolesResponse> GetRolesAsync(CancellationToken ct = default)
    {
        ValidateTenantContext();
        await using var db = _dbFactory.Create();

        var roles = await db.Roles
            .Select(r => new RoleListItemDto(
                r.Id,
                r.Name,
                r.Description,
                r.UserRoles.Count,
                r.ModulePermissions.Count(mp => mp.CanView || mp.CanCreate || mp.CanUpdate || mp.CanDelete),
                r.CreatedAt
            ))
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        return new RolesResponse(roles);
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
            .ToList();

        var users = role.UserRoles
            .Select(ur => new RoleUserDto(ur.User.Id, ur.User.Email))
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

        // Validar que no exista
        var exists = await db.Roles.AnyAsync(r => r.Name == request.Name, ct);
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

        // No permitir renombrar roles del sistema
        if (SystemRoles.Contains(role.Name) && role.Name != request.Name)
            throw new InvalidOperationException($"Cannot rename system role '{role.Name}'");

        // Validar que nuevo nombre no exista
        if (role.Name != request.Name)
        {
            var exists = await db.Roles.AnyAsync(r => r.Name == request.Name && r.Id != roleId, ct);
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
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);

        if (role == null)
            throw new InvalidOperationException("Role not found");

        // Protección: no eliminar roles del sistema
        if (SystemRoles.Contains(role.Name))
            throw new InvalidOperationException($"Cannot delete system role '{role.Name}'");

        // Protección: no eliminar roles con usuarios asignados
        if (role.UserRoles.Any())
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it has {role.UserRoles.Count} user(s) assigned");

        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Role deleted: {RoleName} (ID: {RoleId})", role.Name, role.Id);
    }

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

        // Validar que todos los módulos existen
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

        // Agregar nuevos permisos
        foreach (var perm in request.Permissions)
        {
            // Solo crear registro si tiene al menos un permiso activo
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

    private void ValidateTenantContext()
    {
        if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            throw new InvalidOperationException("No tenant context available");
    }
}
```

---

#### **Modificar/extender `TenantAuth/TenantAuthService.cs`** → Agregar:
```csharp
// Agregar al servicio existente (o crear UserManagementService si prefieres separarlo)

public interface IUserManagementService
{
    Task<TenantUserDetailDto> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<TenantUserDetailDto> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<TenantUserDetailDto> UpdateUserActiveStatusAsync(Guid userId, UpdateUserActiveStatusRequest request, CancellationToken ct = default);
    Task DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken ct = default);
}
```

#### **Crear `Users/UserManagementService.cs`:**
```csharp
using CC.Domain.Dto;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Users;

public class UserManagementService : IUserManagementService
{
    private readonly TenantDbContextFactory _dbFactory;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(
        TenantDbContextFactory dbFactory,
        ITenantAccessor tenantAccessor,
        ILogger<UserManagementService> logger)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<TenantUserDetailDto> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        ValidateTenantContext();
        await using var db = _dbFactory.Create();

        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            throw new InvalidOperationException("User not found");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        return new TenantUserDetailDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            roles,
            user.IsActive,
            user.MustChangePassword,
            user.CreatedAt,
            user.UpdatedAt
        );
    }

    public async Task<TenantUserDetailDto> UpdateUserRolesAsync(
        Guid userId,
        UpdateUserRolesRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        ValidateTenantContext();
        await using var db = _dbFactory.Create();

        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            throw new InvalidOperationException("User not found");

        // 🔒 LOCKOUT PROTECTION: Si es el usuario actual modificándose a sí mismo
        if (userId == currentUserId)
        {
            var currentRoles = user.UserRoles.Select(ur => ur.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Verificar que no se quite el rol SuperAdmin si lo tiene
            if (currentRoles.Contains("SuperAdmin") && !request.RoleNames.Contains("SuperAdmin"))
            {
                throw new InvalidOperationException("Cannot remove SuperAdmin role from yourself. This would lock you out.");
            }

            // Verificar que al menos mantenga un rol con permisos admin
            if (!request.RoleNames.Any())
            {
                throw new InvalidOperationException("Cannot remove all roles from yourself. This would lock you out.");
            }
        }

        // Validar que los roles existen
        var rolesToAssign = await db.Roles
            .Where(r => request.RoleNames.Contains(r.Name))
            .ToListAsync(ct);

        if (rolesToAssign.Count != request.RoleNames.Count)
        {
            var foundNames = rolesToAssign.Select(r => r.Name).ToHashSet();
            var missing = request.RoleNames.Except(foundNames).ToList();
            throw new InvalidOperationException($"Roles not found: {string.Join(", ", missing)}");
        }

        // Eliminar roles actuales
        db.UserRoles.RemoveRange(user.UserRoles);

        // Asignar nuevos roles
        foreach (var role in rolesToAssign)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("User roles updated: {Email} → {Roles}", user.Email, string.Join(", ", request.RoleNames));

        return await GetUserByIdAsync(userId, ct);
    }

    public async Task<TenantUserDetailDto> UpdateUserActiveStatusAsync(
        Guid userId,
        UpdateUserActiveStatusRequest request,
        CancellationToken ct = default)
    {
        ValidateTenantContext();
        await using var db = _dbFactory.Create();

        var user = await db.Users.FindAsync(new object[] { userId }, ct);
        if (user == null)
            throw new InvalidOperationException("User not found");

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("User active status updated: {Email} → IsActive={IsActive}", user.Email, user.IsActive);

        return await GetUserByIdAsync(userId, ct);
    }

    public async Task DeleteUserAsync(Guid userId, Guid currentUserId, CancellationToken ct = default)
    {
        ValidateTenantContext();
        await using var db = _dbFactory.Create();

        // 🔒 LOCKOUT PROTECTION: No permitir que un usuario se elimine a sí mismo
        if (userId == currentUserId)
        {
            throw new InvalidOperationException("Cannot delete yourself. This would lock you out.");
        }

        var user = await db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            throw new InvalidOperationException("User not found");

        // Soft delete: solo desactivar
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("User soft-deleted (deactivated): {Email}", user.Email);
    }

    private void ValidateTenantContext()
    {
        if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            throw new InvalidOperationException("No tenant context available");
    }
}
```

---

### **3. Endpoints** (`Api-eCommerce/Endpoints/`)

#### **Crear `RoleAdminEndpoints.cs`:**
```csharp
using Api_eCommerce.Authorization;
using CC.Aplication.Roles;
using CC.Domain.Dto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api_eCommerce.Endpoints;

public static class RoleAdminEndpoints
{
    public static IEndpointRouteBuilder MapRoleAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/roles")
            .RequireAuthorization()
            .AddEndpointFilter<ModuleAuthorizationFilter>()
            .WithTags("Roles Management");

        // ==================== ROLES CRUD ====================
        group.MapGet("", GetRoles)
            .WithName("GetRoles")
            .WithSummary("Get all roles")
            .WithMetadata(new RequireModuleAttribute("settings", "view"))
            .Produces<RolesResponse>(StatusCodes.Status200OK);

        group.MapGet("{id:guid}", GetRoleById)
            .WithName("GetRoleById")
            .WithSummary("Get role by ID with permissions and users")
            .WithMetadata(new RequireModuleAttribute("settings", "view"))
            .Produces<RoleDetailDto>(StatusCodes.Status200OK);

        group.MapPost("", CreateRole)
            .WithName("CreateRole")
            .WithSummary("Create new role")
            .WithMetadata(new RequireModuleAttribute("settings", "create"))
            .Produces<RoleDetailDto>(StatusCodes.Status201Created);

        group.MapPut("{id:guid}", UpdateRole)
            .WithName("UpdateRole")
            .WithSummary("Update role name/description")
            .WithMetadata(new RequireModuleAttribute("settings", "update"))
            .Produces<RoleDetailDto>(StatusCodes.Status200OK);

        group.MapDelete("{id:guid}", DeleteRole)
            .WithName("DeleteRole")
            .WithSummary("Delete role (only if no users assigned)")
            .WithMetadata(new RequireModuleAttribute("settings", "delete"))
            .Produces(StatusCodes.Status204NoContent);

        // ==================== PERMISSIONS ====================
        group.MapGet("/permissions/available", GetAvailableModules)
            .WithName("GetAvailableModules")
            .WithSummary("Get catalog of all available modules")
            .WithMetadata(new RequireModuleAttribute("settings", "view"))
            .Produces<AvailableModulesResponse>(StatusCodes.Status200OK);

        group.MapGet("{id:guid}/permissions", GetRolePermissions)
            .WithName("GetRolePermissions")
            .WithSummary("Get permissions for a specific role")
            .WithMetadata(new RequireModuleAttribute("settings", "view"))
            .Produces<RolePermissionsResponse>(StatusCodes.Status200OK);

        group.MapPut("{id:guid}/permissions", UpdateRolePermissions)
            .WithName("UpdateRolePermissions")
            .WithSummary("Update role permissions (replaces all)")
            .WithMetadata(new RequireModuleAttribute("settings", "update"))
            .Produces<RolePermissionsResponse>(StatusCodes.Status200OK);

        return group;
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> GetRoles(IRoleService roleService, CancellationToken ct)
    {
        try
        {
            var result = await roleService.GetRolesAsync(ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> GetRoleById(
        Guid id,
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            var result = await roleService.GetRoleByIdAsync(id, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> CreateRole(
        [FromBody] CreateRoleRequest request,
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            var result = await roleService.CreateRoleAsync(request, ct);
            return Results.Created($"/admin/roles/{result.Id}", result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateRole(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            var result = await roleService.UpdateRoleAsync(id, request, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> DeleteRole(
        Guid id,
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            await roleService.DeleteRoleAsync(id, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> GetAvailableModules(
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            var result = await roleService.GetAvailableModulesAsync(ct);
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> GetRolePermissions(
        Guid id,
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            var result = await roleService.GetRolePermissionsAsync(id, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateRolePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsRequest request,
        IRoleService roleService,
        CancellationToken ct)
    {
        try
        {
            var result = await roleService.UpdateRolePermissionsAsync(id, request, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }
}
```

#### **Modificar `TenantAdminEndpoints.cs`** → Agregar nuevos endpoints de usuarios:
```csharp
// Agregar después de los endpoints existentes de usuarios

group.MapGet("/users/{id:guid}", GetUserById)
    .WithName("AdminGetUserById")
    .WithSummary("Get user details by ID")
    .WithMetadata(new RequireModuleAttribute("customers", "view"))
    .Produces<TenantUserDetailDto>(StatusCodes.Status200OK);

group.MapPatch("/users/{id:guid}/roles", UpdateUserRoles)
    .WithName("AdminUpdateUserRoles")
    .WithSummary("Update user roles (replaces all)")
    .WithMetadata(new RequireModuleAttribute("customers", "update"))
    .Produces<TenantUserDetailDto>(StatusCodes.Status200OK);

group.MapPatch("/users/{id:guid}/active", UpdateUserActiveStatus)
    .WithName("AdminUpdateUserActiveStatus")
    .WithSummary("Activate or deactivate user")
    .WithMetadata(new RequireModuleAttribute("customers", "update"))
    .Produces<TenantUserDetailDto>(StatusCodes.Status200OK);

group.MapDelete("/users/{id:guid}", DeleteUser)
    .WithName("AdminDeleteUser")
    .WithSummary("Soft delete user (deactivate)")
    .WithMetadata(new RequireModuleAttribute("customers", "delete"))
    .Produces(StatusCodes.Status204NoContent);

// ==================== HANDLERS ====================

private static async Task<IResult> GetUserById(
    Guid id,
    IUserManagementService userService,
    CancellationToken ct)
{
    try
    {
        var result = await userService.GetUserByIdAsync(id, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}

private static async Task<IResult> UpdateUserRoles(
    Guid id,
    [FromBody] UpdateUserRolesRequest request,
    HttpContext httpContext,
    IUserManagementService userService,
    CancellationToken ct)
{
    try
    {
        // Obtener ID del usuario actual del token
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var currentUserId))
        {
            return Results.Problem("User ID not found in token", statusCode: 401);
        }

        var result = await userService.UpdateUserRolesAsync(id, request, currentUserId, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}

private static async Task<IResult> UpdateUserActiveStatus(
    Guid id,
    [FromBody] UpdateUserActiveStatusRequest request,
    IUserManagementService userService,
    CancellationToken ct)
{
    try
    {
        var result = await userService.UpdateUserActiveStatusAsync(id, request, ct);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}

private static async Task<IResult> DeleteUser(
    Guid id,
    HttpContext httpContext,
    IUserManagementService userService,
    CancellationToken ct)
{
    try
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var currentUserId))
        {
            return Results.Problem("User ID not found in token", statusCode: 401);
        }

        await userService.DeleteUserAsync(id, currentUserId, ct);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
}
```

---

### **4. Dependency Injection** (`Program.cs`)

#### **Agregar en la sección de servicios:**
```csharp
// ==================== RBAC SERVICES ====================
builder.Services.AddScoped<CC.Aplication.Roles.IRoleService, CC.Aplication.Roles.RoleService>();
builder.Services.AddScoped<CC.Aplication.Users.IUserManagementService, CC.Aplication.Users.UserManagementService>();
```

#### **Mapear nuevos endpoints:**
```csharp
// ==================== ENDPOINTS ====================
app.MapRoleAdminEndpoints();   // ← NUEVO
app.MapTenantAdminEndpoints();  // Ya existe
```

---

### **5. Corrección del bug JWT** (AdminAuthService)

#### **Modificar `CC.Aplication/Admin/AdminAuthService.cs`:**
```csharp
private string GenerateJwtToken(Guid userId, string email, List<string> roles)
{
    var jwtKey = _configuration["jwtKey"] ?? throw new InvalidOperationException("JWT key not configured");
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    // ✅ AGREGAR ISSUER Y AUDIENCE
    var issuer = _configuration["Jwt:Issuer"] ?? "ecommerce-api";
    var audience = _configuration["Jwt:Audience"] ?? "ecommerce-clients";

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Email, email),
        new Claim("admin", "true"),
        new Claim("jti", Guid.NewGuid().ToString()),
        new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
    };

    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddHours(24),
        SigningCredentials = credentials,
        Issuer = issuer,        // ✅ NUEVO
        Audience = audience     // ✅ NUEVO
    };

    var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}
```

---

## 📝 Orden de Implementación

### **Fase 1: Fundamentos (Día 1)**
1. ✅ Crear DTOs (`RoleDtos.cs`, `RolePermissionDtos.cs`, modificar `UserDto.cs`)
2. ✅ Corregir bug JWT en `AdminAuthService`
3. ✅ Crear `IRoleService` y `RoleService`

### **Fase 2: Endpoints de Roles (Día 1-2)**
4. ✅ Crear `RoleAdminEndpoints.cs`
5. ✅ Registrar servicios y endpoints en `Program.cs`
6. ✅ Probar CRUD de roles con Postman/REST Client

### **Fase 3: Gestión de Usuarios (Día 2)**
7. ✅ Crear `IUserManagementService` y `UserManagementService`
8. ✅ Modificar `TenantAdminEndpoints` con nuevos endpoints
9. ✅ Probar endpoints de usuarios

### **Fase 4: Testing & Documentación (Día 3)**
10. ✅ Actualizar `README.md` con nuevos endpoints
11. ✅ Crear ejemplos de requests/responses
12. ✅ Testing end-to-end completo

---

## 🛡️ Protecciones de Seguridad Implementadas

### **1. Lockout Protection (Usuario)**
- ❌ No puede eliminarse a sí mismo
- ❌ No puede quitarse el rol SuperAdmin si lo tiene
- ❌ No puede quedarse sin roles

### **2. Lockout Protection (Roles)**
- ❌ No se pueden eliminar roles del sistema (`SuperAdmin`, `Customer`)
- ❌ No se pueden renombrar roles del sistema
- ❌ No se pueden eliminar roles con usuarios asignados

### **3. Validaciones de Contexto**
- ✅ Validación de tenant context en todos los servicios
- ✅ Validación de existencia de roles antes de asignar
- ✅ Validación de existencia de módulos antes de asignar permisos

---

## 🧪 Testing Manual

### **Casos de prueba:**

```http
### 1. Crear rol
POST {{baseUrl}}/admin/roles
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}
Content-Type: application/json

{
  "name": "Staff",
  "description": "Empleados de tienda"
}

### 2. Listar roles
GET {{baseUrl}}/admin/roles
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}

### 3. Asignar permisos a rol
PUT {{baseUrl}}/admin/roles/{{roleId}}/permissions
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}
Content-Type: application/json

{
  "permissions": [
    {
      "moduleId": "{{salesModuleId}}",
      "canView": true,
      "canCreate": true,
      "canUpdate": false,
      "canDelete": false
    }
  ]
}

### 4. Obtener detalle de usuario
GET {{baseUrl}}/admin/users/{{userId}}
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}

### 5. Actualizar roles de usuario
PATCH {{baseUrl}}/admin/users/{{userId}}/roles
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}
Content-Type: application/json

{
  "roleNames": ["Staff", "Customer"]
}

### 6. Intentar lockout (debe fallar)
PATCH {{baseUrl}}/admin/users/{{myUserId}}/roles
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}
Content-Type: application/json

{
  "roleNames": []  // ❌ Debe retornar error
}

### 7. Desactivar usuario
PATCH {{baseUrl}}/admin/users/{{userId}}/active
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}
Content-Type: application/json

{
  "isActive": false
}

### 8. Soft delete usuario
DELETE {{baseUrl}}/admin/users/{{userId}}
Authorization: Bearer {{token}}
X-Tenant-Slug: {{slug}}
```

---

## 📊 Resumen de Cambios

| Componente | Archivos Nuevos | Archivos Modificados | Líneas de Código |
|------------|-----------------|----------------------|------------------|
| DTOs       | 2               | 1                    | ~200             |
| Servicios  | 2               | 1                    | ~600             |
| Endpoints  | 1               | 1                    | ~400             |
| DI         | 0               | 1 (Program.cs)       | ~10              |
| **TOTAL**  | **5**           | **4**                | **~1,210**       |

---

## ✅ Checklist Final

- [ ] Todos los DTOs creados
- [ ] `RoleService` implementado y testeado
- [ ] `UserManagementService` implementado y testeado
- [ ] `RoleAdminEndpoints` mapeado y funcionando
- [ ] Endpoints de usuarios extendidos
- [ ] Bug JWT corregido
- [ ] Protecciones de lockout probadas
- [ ] README.md actualizado
- [ ] Testing end-to-end completo
- [ ] Deploy a staging/producción

---

**¿Estás listo para empezar la implementación?** 🚀
