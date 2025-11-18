using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Permissions
{
    public interface IPermissionService
    {
        Task<bool> CanAccessModuleAsync(Guid userId, string moduleCode);
        Task<ModulePermissions> GetUserPermissionsAsync(Guid userId, string moduleCode);
        Task<List<ModuleDto>> GetUserModulesAsync(Guid userId);
    }

    public class PermissionService : IPermissionService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;

        public PermissionService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
        }

        public async Task<bool> CanAccessModuleAsync(Guid userId, string moduleCode)
        {
            if (!_tenantAccessor.HasTenant)
            {
                return false;
            }

            await using var db = _dbFactory.Create();

            var hasAccess = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.ModulePermissions)
                .AnyAsync(rmp => rmp.Module.Code == moduleCode && rmp.Module.IsActive && rmp.CanView);

            return hasAccess;
        }

        public async Task<ModulePermissions> GetUserPermissionsAsync(Guid userId, string moduleCode)
        {
            if (!_tenantAccessor.HasTenant)
            {
                return new ModulePermissions { ModuleCode = moduleCode };
            }

            await using var db = _dbFactory.Create();

            // Obtener el permiso más permisivo si el usuario tiene múltiples roles
            var permissions = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.ModulePermissions)
                .Where(rmp => rmp.Module.Code == moduleCode && rmp.Module.IsActive)
                .ToListAsync();

            if (!permissions.Any())
            {
                return new ModulePermissions { ModuleCode = moduleCode };
            }

            // Si tiene múltiples roles, usar OR lógico (el más permisivo gana)
            return new ModulePermissions
            {
                ModuleCode = moduleCode,
                CanView = permissions.Any(p => p.CanView),
                CanCreate = permissions.Any(p => p.CanCreate),
                CanUpdate = permissions.Any(p => p.CanUpdate),
                CanDelete = permissions.Any(p => p.CanDelete)
            };
        }

        public async Task<List<ModuleDto>> GetUserModulesAsync(Guid userId)
        {
            if (!_tenantAccessor.HasTenant)
            {
                return new List<ModuleDto>();
            }

            await using var db = _dbFactory.Create();

            var modulesWithPermissions = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.ModulePermissions)
                .Where(rmp => rmp.Module.IsActive && rmp.CanView)
                .GroupBy(rmp => new
                {
                    rmp.Module.Id,
                    rmp.Module.Code,
                    rmp.Module.Name,
                    rmp.Module.Description,
                    rmp.Module.IconName
                })
                .Select(g => new
                {
                    Module = g.Key,
                    Permissions = g.ToList()
                })
                .ToListAsync();

            var modules = modulesWithPermissions.Select(m => new ModuleDto
            {
                Code = m.Module.Code,
                Name = m.Module.Name,
                Description = m.Module.Description,
                IconName = m.Module.IconName,
                Permissions = new ModulePermissions
                {
                    ModuleCode = m.Module.Code,
                    CanView = m.Permissions.Any(p => p.CanView),
                    CanCreate = m.Permissions.Any(p => p.CanCreate),
                    CanUpdate = m.Permissions.Any(p => p.CanUpdate),
                    CanDelete = m.Permissions.Any(p => p.CanDelete)
                }
            })
            .OrderBy(m => m.Name)
            .ToList();

            return modules;
        }
    }

    // ==================== DTOs ====================

    public class ModuleDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? IconName { get; set; }
        public ModulePermissions Permissions { get; set; } = new();
    }

    public class ModulePermissions
    {
        public string ModuleCode { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
    }
}
