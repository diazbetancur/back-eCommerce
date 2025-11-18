using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace CC.Infraestructure.Admin
{
    /// <summary>
    /// Seeder para la base de datos administrativa global (AdminDb)
    /// Crea el usuario SuperAdmin del sistema que gestiona todos los tenants
    /// </summary>
    public static class AdminDbSeeder
    {
        /// <summary>
        /// Seed de datos iniciales del AdminDb (roles y superadmin)
        /// Este método es IDEMPOTENTE - puede ejecutarse múltiples veces sin duplicar datos
        /// </summary>
        public static async Task SeedAsync(AdminDbContext adminDb, ILogger? logger = null)
        {
            logger?.LogInformation("?? Starting AdminDb seed...");

            // ==================== 1. SEED ROLES ====================
            await SeedRolesAsync(adminDb, logger);

            // ==================== 2. SEED SUPERADMIN USER ====================
            await SeedSuperAdminAsync(adminDb, logger);

            // ==================== 3. SEED PLANS AND LIMITS ? NUEVO ====================
            await PlanLimitsSeeder.SeedAsync(adminDb, logger);

            logger?.LogInformation("? AdminDb seed completed successfully");
        }

        /// <summary>
        /// Seed de roles administrativos del sistema
        /// </summary>
        private static async Task SeedRolesAsync(AdminDbContext adminDb, ILogger? logger)
        {
            if (await adminDb.AdminRoles.AnyAsync())
            {
                logger?.LogInformation("??  Admin roles already exist, skipping seed");
                return;
            }

            logger?.LogInformation("Creating admin roles...");

            var roles = new[]
            {
                new AdminRole
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = AdminRoleNames.SuperAdmin,
                    Description = "Full system access - Can manage all tenants, users, and system configuration",
                    CreatedAt = DateTime.UtcNow
                },
                new AdminRole
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = AdminRoleNames.TenantManager,
                    Description = "Can create and manage tenants, view reports and analytics",
                    CreatedAt = DateTime.UtcNow
                },
                new AdminRole
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Name = AdminRoleNames.Support,
                    Description = "Support team access - Can view tenant information and assist users",
                    CreatedAt = DateTime.UtcNow
                },
                new AdminRole
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Name = AdminRoleNames.Viewer,
                    Description = "Read-only access - Can view but not modify system data",
                    CreatedAt = DateTime.UtcNow
                }
            };

            adminDb.AdminRoles.AddRange(roles);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created {Count} admin roles", roles.Length);
        }

        /// <summary>
        /// Seed del usuario SuperAdmin inicial
        /// Credenciales por defecto: admin@yourdomain.com / Admin123!
        /// IMPORTANTE: Cambiar la contraseña después del primer login en producción
        /// </summary>
        private static async Task SeedSuperAdminAsync(AdminDbContext adminDb, ILogger? logger)
        {
            const string adminEmail = "admin@yourdomain.com";

            // Verificar si ya existe un admin
            if (await adminDb.AdminUsers.AnyAsync(u => u.Email == adminEmail))
            {
                logger?.LogInformation("??  SuperAdmin user already exists, skipping seed");
                return;
            }

            logger?.LogInformation("Creating SuperAdmin user...");

            // Obtener rol SuperAdmin
            var superAdminRole = await adminDb.AdminRoles
                .FirstOrDefaultAsync(r => r.Name == AdminRoleNames.SuperAdmin);

            if (superAdminRole == null)
            {
                logger?.LogError("? SuperAdmin role not found! Run SeedRolesAsync first");
                throw new InvalidOperationException("SuperAdmin role not found. Roles must be seeded before users.");
            }

            // Generar hash de la contraseña por defecto
            var (hash, salt) = HashPassword("Admin123!");

            // Crear usuario SuperAdmin
            var superAdmin = new AdminUser
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Email = adminEmail,
                PasswordHash = hash,
                PasswordSalt = salt,
                FullName = "Super Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            adminDb.AdminUsers.Add(superAdmin);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created SuperAdmin user: {Email}", adminEmail);

            // Asignar rol SuperAdmin
            var userRole = new AdminUserRole
            {
                AdminUserId = superAdmin.Id,
                AdminRoleId = superAdminRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            adminDb.AdminUserRoles.Add(userRole);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Assigned SuperAdmin role");
            logger?.LogWarning("??  DEFAULT CREDENTIALS - Email: {Email} | Password: Admin123!", adminEmail);
            logger?.LogWarning("??  IMPORTANT: Change the password after first login in production!");
        }

        /// <summary>
        /// Crea un usuario administrativo adicional (útil para desarrollo o múltiples admins)
        /// </summary>
        public static async Task CreateAdminUserAsync(
            AdminDbContext adminDb,
            string email,
            string password,
            string fullName,
            string roleName,
            ILogger? logger = null)
        {
            // Verificar si el usuario ya existe
            if (await adminDb.AdminUsers.AnyAsync(u => u.Email == email))
            {
                logger?.LogWarning("??  User {Email} already exists", email);
                return;
            }

            // Buscar rol
            var role = await adminDb.AdminRoles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null)
            {
                logger?.LogError("? Role {RoleName} not found", roleName);
                throw new InvalidOperationException($"Role '{roleName}' not found");
            }

            // Crear usuario
            var (hash, salt) = HashPassword(password);

            var user = new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                FullName = fullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            adminDb.AdminUsers.Add(user);
            await adminDb.SaveChangesAsync();

            // Asignar rol
            var userRole = new AdminUserRole
            {
                AdminUserId = user.Id,
                AdminRoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            };

            adminDb.AdminUserRoles.Add(userRole);
            await adminDb.SaveChangesAsync();

            logger?.LogInformation("? Created admin user: {Email} with role {Role}", email, roleName);
        }

        // ==================== PASSWORD HASHING ====================

        public static (string Hash, string Salt) HashPassword(string password)
        {
            var saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);

            return (
                Convert.ToBase64String(hashBytes),
                Convert.ToBase64String(saltBytes)
            );
        }
    }
}
