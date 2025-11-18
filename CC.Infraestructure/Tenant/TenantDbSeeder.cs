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
        /// Seed de datos iniciales de un tenant (roles, categorías, admin user)
        /// Este método es IDEMPOTENTE - puede ejecutarse múltiples veces sin duplicar datos
        /// </summary>
        public static async Task SeedAsync(
            TenantDbContext tenantDb, 
            string tenantSlug,
            ILogger? logger = null)
        {
            logger?.LogInformation("?? Starting TenantDb seed for tenant: {TenantSlug}", tenantSlug);

            // ==================== 1. SEED ROLES ====================
            await SeedRolesAsync(tenantDb, logger);

            // ==================== 2. SEED ADMIN USER ====================
            await SeedTenantAdminAsync(tenantDb, tenantSlug, logger);

            // ==================== 3. SEED DEMO CATEGORIES (Opcional) ====================
            // await SeedDemoCategoriesAsync(tenantDb, logger);

            logger?.LogInformation("? TenantDb seed completed for tenant: {TenantSlug}", tenantSlug);
        }

        /// <summary>
        /// Seed de roles del tenant (Admin, Manager, Customer)
        /// </summary>
        private static async Task SeedRolesAsync(TenantDbContext tenantDb, ILogger? logger)
        {
            if (await tenantDb.Roles.AnyAsync())
            {
                logger?.LogInformation("??  Tenant roles already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating tenant roles...");

            var roles = new[]
            {
                new TenantRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Admin"
                },
                new TenantRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Manager"
                },
                new TenantRole
                {
                    Id = Guid.NewGuid(),
                    Name = "Customer"
                }
            };

            tenantDb.Roles.AddRange(roles);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} tenant roles", roles.Length);
        }

        /// <summary>
        /// Seed del usuario administrador del tenant
        /// Credenciales: admin@{tenantSlug} / TenantAdmin123!
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
                logger?.LogInformation("??  Tenant admin user already exists, skipping seed");
                return;
            }

            logger?.LogInformation("Creating tenant admin user...");

            // Obtener rol Admin
            var adminRole = await tenantDb.Roles
                .FirstOrDefaultAsync(r => r.Name == "Admin");

            if (adminRole == null)
            {
                logger?.LogError("? Admin role not found! Run SeedRolesAsync first");
                throw new InvalidOperationException("Admin role not found. Roles must be seeded before users.");
            }

            // Generar contraseña única por tenant
            var password = $"TenantAdmin123!";
            
            // Hash de contraseña usando Identity PasswordHasher
            var hasher = new PasswordHasher<TenantUser>();
            var passwordHash = hasher.HashPassword(null!, password);

            // Crear usuario admin del tenant
            var tenantAdmin = new TenantUser
            {
                Id = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            tenantDb.Users.Add(tenantAdmin);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("? Created tenant admin user: {Email}", adminEmail);

            // Asignar rol Admin
            var userRole = new TenantUserRole
            {
                UserId = tenantAdmin.Id,
                RoleId = adminRole.Id
            };

            tenantDb.UserRoles.Add(userRole);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("? Assigned Admin role to tenant user");
            logger?.LogWarning("??  TENANT ADMIN CREDENTIALS - Email: {Email} | Password: {Password}", adminEmail, password);
            logger?.LogWarning("??  IMPORTANT: Tenant admin should change password after first login!");
        }

        /// <summary>
        /// Seed de categorías de demostración (opcional)
        /// Útil para development y testing
        /// </summary>
        private static async Task SeedDemoCategoriesAsync(TenantDbContext tenantDb, ILogger? logger)
        {
            if (await tenantDb.Categories.AnyAsync())
            {
                logger?.LogInformation("??  Categories already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating demo categories...");

            var categories = new[]
            {
                new Category
                {
                    Id = Guid.NewGuid(),
                    Name = "Electronics",
                    Description = "Electronic devices and accessories"
                },
                new Category
                {
                    Id = Guid.NewGuid(),
                    Name = "Clothing",
                    Description = "Apparel and fashion items"
                },
                new Category
                {
                    Id = Guid.NewGuid(),
                    Name = "Home & Garden",
                    Description = "Home decor and garden supplies"
                }
            };

            tenantDb.Categories.AddRange(categories);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} demo categories", categories.Length);
        }

        /// <summary>
        /// Crea un usuario adicional para el tenant (útil para crear staff, managers, etc.)
        /// </summary>
        public static async Task CreateTenantUserAsync(
            TenantDbContext tenantDb,
            string email,
            string password,
            string roleName,
            ILogger? logger = null)
        {
            // Verificar si el usuario ya existe
            if (await tenantDb.Users.AnyAsync(u => u.Email == email))
            {
                logger?.LogWarning("??  User {Email} already exists in tenant", email);
                return;
            }

            // Buscar rol
            var role = await tenantDb.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                logger?.LogError("? Role {RoleName} not found in tenant", roleName);
                throw new InvalidOperationException($"Role '{roleName}' not found");
            }

            // Crear usuario
            var hasher = new PasswordHasher<TenantUser>();
            var passwordHash = hasher.HashPassword(null!, password);

            var user = new TenantUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            tenantDb.Users.Add(user);
            await tenantDb.SaveChangesAsync();

            // Asignar rol
            var userRole = new TenantUserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            };

            tenantDb.UserRoles.Add(userRole);
            await tenantDb.SaveChangesAsync();

            logger?.LogInformation("? Created tenant user: {Email} with role {Role}", email, roleName);
        }
    }
}
