using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.TenantSeeders
{
    /// <summary>
    /// Seeder para módulos y permisos del tenant
    /// Se ejecuta al crear un nuevo tenant
    /// </summary>
    public static class TenantModulesSeeder
    {
        public static async Task SeedAsync(TenantDbContext db, ILogger? logger = null)
        {
            logger?.LogInformation("?? Seeding tenant modules and permissions...");

            await SeedModulesAsync(db, logger);
            await SeedRolePermissionsAsync(db, logger);

            logger?.LogInformation("? Tenant modules and permissions seeded successfully");
        }

        private static async Task SeedModulesAsync(TenantDbContext db, ILogger? logger)
        {
            if (await db.Modules.AnyAsync())
            {
                logger?.LogInformation("??  Modules already exist, skipping");
                return;
            }

            logger?.LogInformation("Creating system modules...");

            var modules = new[]
            {
                new Module
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Code = "sales",
                    Name = "Punto de Venta",
                    Description = "Gestión de ventas y órdenes",
                    IconName = "shopping-cart",
                    IsActive = true
                },
                new Module
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Code = "inventory",
                    Name = "Inventario",
                    Description = "Gestión de productos y stock",
                    IconName = "box",
                    IsActive = true
                },
                new Module
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Code = "customers",
                    Name = "Clientes",
                    Description = "Gestión de clientes y usuarios",
                    IconName = "users",
                    IsActive = true
                },
                new Module
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                    Code = "reports",
                    Name = "Reportes",
                    Description = "Reportes y analytics",
                    IconName = "chart-bar",
                    IsActive = true
                },
                new Module
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                    Code = "settings",
                    Name = "Configuración",
                    Description = "Configuración del tenant",
                    IconName = "cog",
                    IsActive = true
                }
            };

            db.Modules.AddRange(modules);
            await db.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} modules", modules.Length);
        }

        private static async Task SeedRolePermissionsAsync(TenantDbContext db, ILogger? logger)
        {
            if (await db.RoleModulePermissions.AnyAsync())
            {
                logger?.LogInformation("??  Role permissions already exist, skipping");
                return;
            }

            logger?.LogInformation("Creating role permissions...");

            var roles = await db.Roles.ToListAsync();
            var modules = await db.Modules.ToListAsync();

            if (!roles.Any() || !modules.Any())
            {
                logger?.LogWarning("??  Roles or modules not found, skipping permissions seed");
                return;
            }

            var adminRole = roles.FirstOrDefault(r => r.Name == "Admin");
            var managerRole = roles.FirstOrDefault(r => r.Name == "Manager");
            var viewerRole = roles.FirstOrDefault(r => r.Name == "Viewer");

            var permissions = new List<RoleModulePermission>();

            // ADMIN - Acceso completo a todo
            if (adminRole != null)
            {
                foreach (var module in modules)
                {
                    permissions.Add(new RoleModulePermission
                    {
                        RoleId = adminRole.Id,
                        ModuleId = module.Id,
                        CanView = true,
                        CanCreate = true,
                        CanUpdate = true,
                        CanDelete = true
                    });
                }
            }

            // MANAGER - Puede ver todo, crear/editar en sales e inventory, sin delete
            if (managerRole != null)
            {
                var salesModule = modules.First(m => m.Code == "sales");
                var inventoryModule = modules.First(m => m.Code == "inventory");
                var customersModule = modules.First(m => m.Code == "customers");
                var reportsModule = modules.First(m => m.Code == "reports");

                permissions.AddRange(new[]
                {
                    new RoleModulePermission
                    {
                        RoleId = managerRole.Id,
                        ModuleId = salesModule.Id,
                        CanView = true,
                        CanCreate = true,
                        CanUpdate = true,
                        CanDelete = false
                    },
                    new RoleModulePermission
                    {
                        RoleId = managerRole.Id,
                        ModuleId = inventoryModule.Id,
                        CanView = true,
                        CanCreate = true,
                        CanUpdate = true,
                        CanDelete = false
                    },
                    new RoleModulePermission
                    {
                        RoleId = managerRole.Id,
                        ModuleId = customersModule.Id,
                        CanView = true,
                        CanCreate = false,
                        CanUpdate = false,
                        CanDelete = false
                    },
                    new RoleModulePermission
                    {
                        RoleId = managerRole.Id,
                        ModuleId = reportsModule.Id,
                        CanView = true,
                        CanCreate = false,
                        CanUpdate = false,
                        CanDelete = false
                    }
                });
            }

            // VIEWER - Solo lectura en sales e inventory
            if (viewerRole != null)
            {
                var salesModule = modules.First(m => m.Code == "sales");
                var inventoryModule = modules.First(m => m.Code == "inventory");

                permissions.AddRange(new[]
                {
                    new RoleModulePermission
                    {
                        RoleId = viewerRole.Id,
                        ModuleId = salesModule.Id,
                        CanView = true,
                        CanCreate = false,
                        CanUpdate = false,
                        CanDelete = false
                    },
                    new RoleModulePermission
                    {
                        RoleId = viewerRole.Id,
                        ModuleId = inventoryModule.Id,
                        CanView = true,
                        CanCreate = false,
                        CanUpdate = false,
                        CanDelete = false
                    }
                });
            }

            db.RoleModulePermissions.AddRange(permissions);
            await db.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} role permissions", permissions.Count);
        }
    }
}
