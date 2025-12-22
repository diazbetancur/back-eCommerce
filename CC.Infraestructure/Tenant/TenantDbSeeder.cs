using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Tenant
{
    /// <summary>
    /// Seeder para la base de datos de cada tenant individual (TenantDb)
    /// Crea el usuario administrador del tenant que gestiona su tienda
    /// </summary>
    public static class TenantDbSeeder
    {
        /// <summary>
        /// Seed de datos iniciales de un tenant (roles, admin user)
        /// Este método es IDEMPOTENTE - puede ejecutarse múltiples veces sin duplicar datos
        /// </summary>
        public static async Task SeedAsync(
            TenantDbContext tenantDb,
            string tenantSlug,
            ILogger? logger = null)
        {
            logger?.LogInformation("🌱 Starting TenantDb seed for tenant: {TenantSlug}", tenantSlug);

            // ==================== 1. SEED ROLES ====================
            await SeedRolesAsync(tenantDb, logger);

            // ==================== 2. SEED MODULES ====================
            await SeedModulesAsync(tenantDb, logger);

            // ==================== 3. SEED ROLE PERMISSIONS ====================
            await SeedRolePermissionsAsync(tenantDb, logger);

            // ==================== 4. SEED ADMIN USER ====================
            await SeedTenantAdminAsync(tenantDb, tenantSlug, logger);

            logger?.LogInformation("✅ TenantDb seed completed for tenant: {TenantSlug}", tenantSlug);
        }

        /// <summary>
        /// Seed de roles del tenant (SuperAdmin, Customer)
        /// El tenant puede crear roles adicionales después
        /// </summary>
        public static async Task SeedRolesAsync(TenantDbContext tenantDb, ILogger? logger)
        {
            if (await tenantDb.Roles.AnyAsync())
            {
                logger?.LogInformation("⚠️  Tenant roles already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating tenant roles...");

            var roles = new[]
            {
                new Role
                {
                    Id = Guid.NewGuid(),
                    Name = "SuperAdmin",
                    Description = "Administrador con acceso total al sistema",
                    CreatedAt = DateTime.UtcNow
                },
                new Role
                {
                    Id = Guid.NewGuid(),
                    Name = "Customer",
                    Description = "Cliente con acceso a compras y perfil",
                    CreatedAt = DateTime.UtcNow
                }
            };

            tenantDb.Roles.AddRange(roles);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("✅ Created {Count} tenant roles: {Roles}",
                roles.Length, string.Join(", ", roles.Select(r => r.Name)));
        }

        /// <summary>
        /// Seed del usuario administrador del tenant
        /// Credenciales: admin@{tenantSlug} / TenantAdmin123!
        /// Datos: FirstName="Admin", LastName="System"
        /// </summary>
        private static async Task SeedTenantAdminAsync(
            TenantDbContext tenantDb,
            string tenantSlug,
            ILogger? logger)
        {
            var adminEmail = $"admin@{tenantSlug}";

            // Verificar si ya existe un admin para este tenant
            if (await tenantDb.Users.AnyAsync(u => u.Email == adminEmail))
            {
                logger?.LogInformation("⚠️  Tenant admin user already exists, skipping seed");
                return;
            }

            logger?.LogInformation("Creating tenant admin user...");

            // Obtener rol SuperAdmin
            var adminRole = await tenantDb.Roles
                .FirstOrDefaultAsync(r => r.Name == "SuperAdmin");

            if (adminRole == null)
            {
                logger?.LogError("❌ SuperAdmin role not found! Run SeedRolesAsync first");
                throw new InvalidOperationException("SuperAdmin role not found. Roles must be seeded before users.");
            }

            // Generar contraseña única por tenant
            var password = $"TenantAdmin123!";

            // Hash de contraseña usando Identity PasswordHasher
            var hasher = new PasswordHasher<User>();
            var passwordHash = hasher.HashPassword(null!, password);

            // Crear usuario admin del tenant
            var tenantAdmin = new User
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = passwordHash,
                FirstName = "Admin",
                LastName = "System",
                PhoneNumber = null,
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };

            tenantDb.Users.Add(tenantAdmin);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("✅ Created tenant admin user: {Email}", adminEmail);

            // Asignar rol SuperAdmin
            var userRole = new UserRole
            {
                UserId = tenantAdmin.Id,
                RoleId = adminRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            tenantDb.UserRoles.Add(userRole);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("✅ Assigned SuperAdmin role to tenant user");
            logger?.LogWarning("🔑 TENANT ADMIN CREDENTIALS - Email: {Email} | Password: {Password}", adminEmail, password);
            logger?.LogWarning("⚠️  IMPORTANT: Tenant admin should change password after first login!");
        }

        /// <summary>
        /// Seed de módulos del sistema
        /// Define las áreas funcionales disponibles en el tenant
        /// </summary>
        public static async Task SeedModulesAsync(TenantDbContext tenantDb, ILogger? logger)
        {
            if (await tenantDb.Modules.AnyAsync())
            {
                logger?.LogInformation("⚠️  Modules already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating system modules...");

            var modules = new[]
            {
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "dashboard",
                    Name = "Dashboard",
                    Description = "Panel de control y estadísticas generales",
                    IconName = "chart-line",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "catalog",
                    Name = "Catálogo",
                    Description = "Gestión de productos y categorías",
                    IconName = "box",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "orders",
                    Name = "Pedidos",
                    Description = "Gestión de órdenes y ventas",
                    IconName = "shopping-cart",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "customers",
                    Name = "Clientes",
                    Description = "Gestión de clientes y perfiles",
                    IconName = "users",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "loyalty",
                    Name = "Programa de Lealtad",
                    Description = "Gestión de puntos y recompensas",
                    IconName = "star",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "settings",
                    Name = "Configuración",
                    Description = "Configuración del tenant",
                    IconName = "cog",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Module
                {
                    Id = Guid.NewGuid(),
                    Code = "permissions",
                    Name = "Permisos",
                    Description = "Gestión de roles y permisos",
                    IconName = "shield",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            tenantDb.Modules.AddRange(modules);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("✅ Created {Count} modules: {Modules}",
                modules.Length, string.Join(", ", modules.Select(m => m.Code)));
        }

        /// <summary>
        /// Seed de permisos por rol sobre los módulos
        /// SuperAdmin: acceso total a todos los módulos
        /// Customer: solo acceso de lectura a catálogo y órdenes
        /// </summary>
        public static async Task SeedRolePermissionsAsync(TenantDbContext tenantDb, ILogger? logger)
        {
            if (await tenantDb.RoleModulePermissions.AnyAsync())
            {
                logger?.LogInformation("⚠️  Role permissions already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating role permissions...");

            var superAdminRole = await tenantDb.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
            var customerRole = await tenantDb.Roles.FirstOrDefaultAsync(r => r.Name == "Customer");
            var modules = await tenantDb.Modules.ToListAsync();

            if (superAdminRole == null || customerRole == null)
            {
                logger?.LogError("❌ Required roles not found");
                throw new InvalidOperationException("Roles must be seeded before permissions");
            }

            if (!modules.Any())
            {
                logger?.LogError("❌ No modules found");
                throw new InvalidOperationException("Modules must be seeded before permissions");
            }

            var permissions = new List<RoleModulePermission>();

            // SuperAdmin: acceso completo a todos los módulos
            foreach (var module in modules)
            {
                permissions.Add(new RoleModulePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = superAdminRole.Id,
                    ModuleId = module.Id,
                    CanView = true,
                    CanCreate = true,
                    CanUpdate = true,
                    CanDelete = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Customer: acceso limitado
            var customerModules = modules.Where(m =>
                m.Code == "catalog" ||
                m.Code == "orders" ||
                m.Code == "loyalty" ||
                m.Code == "permissions").ToList(); // Agregado permissions para que el usuario pueda ver sus propios permisos

            foreach (var module in customerModules)
            {
                var canCreate = module.Code == "orders"; // Solo puede crear órdenes

                permissions.Add(new RoleModulePermission
                {
                    Id = Guid.NewGuid(),
                    RoleId = customerRole.Id,
                    ModuleId = module.Id,
                    CanView = true,
                    CanCreate = canCreate,
                    CanUpdate = false,
                    CanDelete = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            tenantDb.RoleModulePermissions.AddRange(permissions);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("✅ Created {Count} role permissions", permissions.Count);
            logger?.LogInformation("   - SuperAdmin: Full access to {ModuleCount} modules", modules.Count);
            logger?.LogInformation("   - Customer: Limited access to {ModuleCount} modules", customerModules.Count);
        }

        /// <summary>
        /// Crea un usuario adicional para el tenant (útil para crear staff, customers, etc.)
        /// </summary>
        public static async Task CreateTenantUserAsync(
            TenantDbContext tenantDb,
            string email,
            string password,
            string roleName,
            string firstName,
            string lastName,
            ILogger? logger = null)
        {
            // Verificar si el usuario ya existe
            if (await tenantDb.Users.AnyAsync(u => u.Email == email))
            {
                logger?.LogWarning("⚠️  User {Email} already exists in tenant", email);
                return;
            }

            // Buscar rol
            var role = await tenantDb.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                logger?.LogError("❌ Role {RoleName} not found in tenant", roleName);
                throw new InvalidOperationException($"Role '{roleName}' not found");
            }

            // Crear usuario
            var hasher = new PasswordHasher<User>();
            var passwordHash = hasher.HashPassword(null!, password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };

            tenantDb.Users.Add(user);
            await tenantDb.SaveChangesAsync();

            // Asignar rol
            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            };

            tenantDb.UserRoles.Add(userRole);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("✅ Created tenant user: {Email} with role {Role}", email, roleName);
        }
    }
}
