using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.TenantSeeders
{
    /// <summary>
    /// Seeder para módulos y permisos del tenant.
    /// IMPORTANTE: Este seeder AUTO-SINCRONIZA módulos y permisos.
    /// - Detecta módulos nuevos y los agrega automáticamente
    /// - Asigna permisos completos al rol Admin para módulos nuevos
    /// - Es seguro ejecutar múltiples veces (idempotente)
    /// 
    /// Para agregar un nuevo módulo:
    /// 1. Agrégalo a GetSystemModules()
    /// 2. Configura permisos en GetRolePermissionDefinitions()
    /// 3. Ejecuta el seeder (se auto-sincroniza)
    /// </summary>
    public static class TenantModulesSeeder
    {
        #region Module Definitions - Source of Truth

        /// <summary>
        /// FUENTE DE VERDAD para todos los módulos del sistema.
        /// Para agregar un nuevo módulo, solo agrega una entrada aquí.
        /// </summary>
        private static readonly ModuleDefinition[] SystemModules = new[]
        {
            new ModuleDefinition("00000000-0000-0000-0000-000000000001", "sales", "Punto de Venta", "Gestión de ventas y órdenes", "shopping-cart"),
            new ModuleDefinition("00000000-0000-0000-0000-000000000002", "inventory", "Inventario", "Gestión de productos y stock", "box"),
            new ModuleDefinition("00000000-0000-0000-0000-000000000003", "customers", "Clientes", "Gestión de clientes y usuarios", "users"),
            new ModuleDefinition("00000000-0000-0000-0000-000000000004", "reports", "Reportes", "Reportes y analytics", "chart-bar"),
            new ModuleDefinition("00000000-0000-0000-0000-000000000005", "settings", "Configuración", "Configuración de la tienda", "cog"),
            new ModuleDefinition("00000000-0000-0000-0000-000000000006", "loyalty", "Fidelización", "Programa de puntos y recompensas", "gift"),
            new ModuleDefinition("00000000-0000-0000-0000-000000000007", "marketing", "Marketing", "Banners, promociones y campañas", "megaphone"),
        };

        /// <summary>
        /// Define permisos por defecto para cada rol y módulo.
        /// Si no se especifica un módulo para un rol, ese rol NO tendrá acceso.
        /// </summary>
        private static Dictionary<string, Dictionary<string, PermissionSet>> GetRolePermissionDefinitions()
        {
            return new Dictionary<string, Dictionary<string, PermissionSet>>
            {
                // ADMIN - Acceso completo a todo
                ["Admin"] = SystemModules.ToDictionary(
                    m => m.Code,
                    _ => new PermissionSet(true, true, true, true)
                ),

                // MANAGER - Permisos operativos
                ["Manager"] = new Dictionary<string, PermissionSet>
                {
                    ["sales"] = new(CanView: true, CanCreate: true, CanUpdate: true, CanDelete: false),
                    ["inventory"] = new(CanView: true, CanCreate: true, CanUpdate: true, CanDelete: false),
                    ["customers"] = new(CanView: true, CanCreate: false, CanUpdate: false, CanDelete: false),
                    ["reports"] = new(CanView: true, CanCreate: false, CanUpdate: false, CanDelete: false),
                    ["loyalty"] = new(CanView: true, CanCreate: true, CanUpdate: true, CanDelete: false),
                },

                // VIEWER - Solo lectura
                ["Viewer"] = new Dictionary<string, PermissionSet>
                {
                    ["sales"] = new(CanView: true, CanCreate: false, CanUpdate: false, CanDelete: false),
                    ["inventory"] = new(CanView: true, CanCreate: false, CanUpdate: false, CanDelete: false),
                }
            };
        }

        #endregion

        public static async Task SeedAsync(TenantDbContext db, ILogger? logger = null)
        {
            logger?.LogInformation("🔧 Seeding tenant modules and permissions...");

            var syncResult = await SyncModulesAsync(db, logger);
            await SyncRolePermissionsAsync(db, logger, syncResult.NewModules);

            logger?.LogInformation("✅ Tenant modules and permissions synced successfully");
        }

        /// <summary>
        /// Sincroniza módulos: agrega nuevos, actualiza existentes si es necesario.
        /// NO elimina módulos (para preservar datos históricos).
        /// </summary>
        private static async Task<ModuleSyncResult> SyncModulesAsync(TenantDbContext db, ILogger? logger)
        {
            var existingModules = await db.Modules.ToDictionaryAsync(m => m.Code, m => m);
            var newModules = new List<Module>();
            var updatedCount = 0;

            foreach (var def in SystemModules)
            {
                if (existingModules.TryGetValue(def.Code, out var existing))
                {
                    // Actualizar si cambió el nombre o descripción
                    if (existing.Name != def.Name || existing.Description != def.Description || existing.IconName != def.Icon)
                    {
                        existing.Name = def.Name;
                        existing.Description = def.Description;
                        existing.IconName = def.Icon;
                        updatedCount++;
                    }
                }
                else
                {
                    // Módulo nuevo - agregar
                    var newModule = new Module
                    {
                        Id = Guid.Parse(def.Id),
                        Code = def.Code,
                        Name = def.Name,
                        Description = def.Description,
                        IconName = def.Icon,
                        IsActive = true
                    };
                    db.Modules.Add(newModule);
                    newModules.Add(newModule);
                    logger?.LogInformation("➕ New module detected: {Code} - {Name}", def.Code, def.Name);
                }
            }

            if (newModules.Any() || updatedCount > 0)
            {
                await db.SaveChangesAsync();
                logger?.LogInformation("📦 Modules synced: {New} new, {Updated} updated", newModules.Count, updatedCount);
            }
            else
            {
                logger?.LogInformation("📦 All modules up to date");
            }

            return new ModuleSyncResult(newModules);
        }

        /// <summary>
        /// Sincroniza permisos de roles:
        /// - Asigna permisos a nuevos módulos
        /// - El rol Admin SIEMPRE obtiene acceso completo a módulos nuevos
        /// </summary>
        private static async Task SyncRolePermissionsAsync(TenantDbContext db, ILogger? logger, List<Module> newModules)
        {
            var roles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r);
            var existingPermissions = await db.RoleModulePermissions.ToListAsync();
            var allModules = await db.Modules.ToDictionaryAsync(m => m.Code, m => m);
            var permissionDefinitions = GetRolePermissionDefinitions();

            var permissionsToAdd = new List<RoleModulePermission>();

            foreach (var (roleName, modulePermissions) in permissionDefinitions)
            {
                if (!roles.TryGetValue(roleName, out var role))
                {
                    logger?.LogWarning("⚠️ Role '{Role}' not found, skipping permissions", roleName);
                    continue;
                }

                foreach (var (moduleCode, permSet) in modulePermissions)
                {
                    if (!allModules.TryGetValue(moduleCode, out var module))
                        continue;

                    // Verificar si ya existe el permiso
                    var existingPerm = existingPermissions.FirstOrDefault(p =>
                        p.RoleId == role.Id && p.ModuleId == module.Id);

                    if (existingPerm == null)
                    {
                        permissionsToAdd.Add(new RoleModulePermission
                        {
                            RoleId = role.Id,
                            ModuleId = module.Id,
                            CanView = permSet.CanView,
                            CanCreate = permSet.CanCreate,
                            CanUpdate = permSet.CanUpdate,
                            CanDelete = permSet.CanDelete
                        });
                        logger?.LogInformation("➕ Adding permission: {Role} -> {Module}", roleName, moduleCode);
                    }
                }
            }

            // IMPORTANTE: Admin SIEMPRE obtiene acceso completo a módulos nuevos
            // (incluso si no están en las definiciones explícitas)
            if (roles.TryGetValue("Admin", out var adminRole) && newModules.Any())
            {
                foreach (var newModule in newModules)
                {
                    var alreadyAdded = permissionsToAdd.Any(p =>
                        p.RoleId == adminRole.Id && p.ModuleId == newModule.Id);

                    if (!alreadyAdded)
                    {
                        permissionsToAdd.Add(new RoleModulePermission
                        {
                            RoleId = adminRole.Id,
                            ModuleId = newModule.Id,
                            CanView = true,
                            CanCreate = true,
                            CanUpdate = true,
                            CanDelete = true
                        });
                        logger?.LogInformation("🔐 Auto-granting Admin full access to new module: {Module}", newModule.Code);
                    }
                }
            }

            if (permissionsToAdd.Any())
            {
                db.RoleModulePermissions.AddRange(permissionsToAdd);
                await db.SaveChangesAsync();
                logger?.LogInformation("🔐 Added {Count} new role permissions", permissionsToAdd.Count);
            }
            else
            {
                logger?.LogInformation("🔐 All role permissions up to date");
            }
        }

        #region Helper Types

        private record ModuleDefinition(string Id, string Code, string Name, string Description, string Icon);
        private record PermissionSet(bool CanView, bool CanCreate, bool CanUpdate, bool CanDelete);
        private record ModuleSyncResult(List<Module> NewModules);

        #endregion
    }
}
